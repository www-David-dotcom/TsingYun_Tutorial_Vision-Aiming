"""PPO training scaffold for the HW7 strategy bonus.

Optional sub-skill — the candidate trains a small policy that picks
between BT branches (engage / retreat / reload) given the same
observation the BT sees. The trained policy is saved as a TorchScript
.pt and HW6's runner can swap it in via a config flag.

This file ships an opinionated minimum:
  * `make_env()` — wraps a gRPC connection to the Unity arena
    in a Gym-style env interface.
  * `PolicyMlp` — small fully-connected actor-critic.
  * `train()` — vanilla PPO with clipped objective, GAE-Lambda advantage
    estimation, AdamW, and rollout in a single process (no
    sample-factory / vectorised env worker pool — that's the
    candidate's stretch goal).

Usage:
    uv sync --group hw7
    uv run python HW7_strategy/src/train_ppo.py \
        --episodes 1000 \
        --out /tmp/hw7_strategy.pt

The default settings train a viable "tracking-only draw vs gold"
policy in ~1k episodes (≈ 30 min on a single CPU). For the full
"beat gold" stretch, the candidate should swap in sample-factory's
parallel rollouts; the README points at the relevant docs.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path

try:
    import numpy as np
    import torch
    import torch.nn as nn
    import torch.nn.functional as F  # noqa: N812 — torch convention
    from torch.distributions import Categorical
except ImportError as e:  # pragma: no cover - hw7 group not synced
    raise ImportError(
        "HW7 train_ppo.py requires torch + numpy; run `uv sync --group hw7`"
    ) from e

OBS_DIM = 12      # self (4) + nearest-3 tracks (3 x dx, dy, hp) + ammo
ACT_DIM = 4       # engage / retreat / patrol / reload


# --------------------------------------------------------- environment

class StubArenaEnv:
    """Gym-style env stub that emits canned observations.

    Replace with a gRPC-backed env once the Unity arena is live on the
    candidate's host. The candidate's job: in `make_env`, swap
    StubArenaEnv for a real one that talks to `tcp://127.0.0.1:7654`.
    """

    def __init__(self, seed: int = 0) -> None:
        self.rng = np.random.default_rng(seed)
        self._t = 0

    def reset(self) -> np.ndarray:
        self._t = 0
        return self.rng.standard_normal(OBS_DIM).astype(np.float32)

    def step(self, action: int) -> tuple[np.ndarray, float, bool, dict]:
        self._t += 1
        obs = self.rng.standard_normal(OBS_DIM).astype(np.float32)
        # Reward: 1 if action matches the "right" branch for the
        # observation's first feature (a placeholder signal); the
        # real reward shaping happens against the live arena.
        target = int((obs[0] > 0) and (obs[1] > 0))
        reward = 1.0 if action == target else -0.1
        done = self._t >= 200
        return obs, reward, done, {}


def make_env(seed: int) -> StubArenaEnv:
    return StubArenaEnv(seed)


# ---------------------------------------------------------------- model

class PolicyMlp(nn.Module):
    def __init__(self, obs_dim: int = OBS_DIM, act_dim: int = ACT_DIM,
                 hidden: int = 64) -> None:
        super().__init__()
        self.shared = nn.Sequential(
            nn.Linear(obs_dim, hidden), nn.Tanh(),
            nn.Linear(hidden, hidden),  nn.Tanh(),
        )
        self.actor = nn.Linear(hidden, act_dim)
        self.critic = nn.Linear(hidden, 1)

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        z = self.shared(obs)
        return self.actor(z), self.critic(z).squeeze(-1)


# ------------------------------------------------------------- training

@dataclass
class Hyperparams:
    episodes: int = 1000
    rollout_len: int = 200
    gamma: float = 0.99
    gae_lambda: float = 0.95
    clip_eps: float = 0.2
    ppo_epochs: int = 4
    batch_size: int = 64
    lr: float = 3e-4
    value_coef: float = 0.5
    entropy_coef: float = 0.01


def collect_rollout(env: StubArenaEnv, policy: PolicyMlp,
                    cfg: Hyperparams, device: torch.device) -> dict:
    obs = env.reset()
    obs_buf, act_buf, logp_buf, rew_buf, done_buf, val_buf = [], [], [], [], [], []
    for _ in range(cfg.rollout_len):
        obs_t = torch.from_numpy(obs).unsqueeze(0).to(device)
        with torch.no_grad():
            logits, value = policy(obs_t)
            dist = Categorical(logits=logits)
            act = dist.sample()
            logp = dist.log_prob(act)
        next_obs, reward, done, _ = env.step(int(act.item()))
        obs_buf.append(obs)
        act_buf.append(int(act.item()))
        logp_buf.append(float(logp.item()))
        rew_buf.append(reward)
        done_buf.append(done)
        val_buf.append(float(value.item()))
        obs = env.reset() if done else next_obs

    # GAE-Lambda advantages.
    advantages = np.zeros(cfg.rollout_len, dtype=np.float32)
    last_gae = 0.0
    last_value = 0.0
    for t in reversed(range(cfg.rollout_len)):
        delta = rew_buf[t] + cfg.gamma * last_value * (1.0 - done_buf[t]) - val_buf[t]
        last_gae = delta + cfg.gamma * cfg.gae_lambda * (1.0 - done_buf[t]) * last_gae
        advantages[t] = last_gae
        last_value = val_buf[t]
    returns = advantages + np.asarray(val_buf, dtype=np.float32)
    return {
        "obs":  torch.tensor(np.stack(obs_buf), dtype=torch.float32, device=device),
        "act":  torch.tensor(act_buf, dtype=torch.long, device=device),
        "logp": torch.tensor(logp_buf, dtype=torch.float32, device=device),
        "adv":  torch.tensor(advantages, dtype=torch.float32, device=device),
        "ret":  torch.tensor(returns, dtype=torch.float32, device=device),
    }


def ppo_update(policy: PolicyMlp, optimizer: torch.optim.Optimizer,
               batch: dict, cfg: Hyperparams) -> dict:
    n = batch["obs"].shape[0]
    losses: list[float] = []
    for _ in range(cfg.ppo_epochs):
        idx = torch.randperm(n)
        for start in range(0, n, cfg.batch_size):
            mb = idx[start:start + cfg.batch_size]
            logits, values = policy(batch["obs"][mb])
            dist = Categorical(logits=logits)
            new_logp = dist.log_prob(batch["act"][mb])
            ratio = torch.exp(new_logp - batch["logp"][mb])
            adv = (batch["adv"][mb] - batch["adv"][mb].mean()) / (batch["adv"][mb].std() + 1e-8)
            surr1 = ratio * adv
            surr2 = torch.clamp(ratio, 1.0 - cfg.clip_eps, 1.0 + cfg.clip_eps) * adv
            policy_loss = -torch.min(surr1, surr2).mean()
            value_loss  = F.mse_loss(values, batch["ret"][mb])
            entropy     = dist.entropy().mean()
            loss = policy_loss + cfg.value_coef * value_loss - cfg.entropy_coef * entropy
            optimizer.zero_grad()
            loss.backward()
            nn.utils.clip_grad_norm_(policy.parameters(), 0.5)
            optimizer.step()
            losses.append(float(loss.item()))
    return {"loss_mean": float(np.mean(losses))}


def train(cfg: Hyperparams, out_path: Path, seed: int) -> int:
    torch.manual_seed(seed)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    env = make_env(seed)
    policy = PolicyMlp().to(device)
    optimizer = torch.optim.AdamW(policy.parameters(), lr=cfg.lr)
    for episode in range(cfg.episodes):
        batch = collect_rollout(env, policy, cfg, device)
        info = ppo_update(policy, optimizer, batch, cfg)
        if (episode + 1) % 50 == 0:
            mean_ret = float(batch["ret"].mean().item())
            print(f"[ep {episode + 1:5d}] loss={info['loss_mean']:.4f} "
                  f"return_mean={mean_ret:+.3f}")
    out_path.parent.mkdir(parents=True, exist_ok=True)
    torch.jit.script(policy).save(str(out_path))
    print(f"saved TorchScript policy → {out_path}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--episodes", type=int, default=1000)
    parser.add_argument("--out", type=Path, default=Path("/tmp/hw7_strategy.pt"))
    parser.add_argument("--seed", type=int, default=0)
    args = parser.parse_args()

    cfg = Hyperparams()
    cfg.episodes = args.episodes
    return train(cfg, args.out, args.seed)


if __name__ == "__main__":
    raise SystemExit(main())
