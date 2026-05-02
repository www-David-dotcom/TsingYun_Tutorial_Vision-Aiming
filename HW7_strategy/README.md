# HW7 — 战术 / Strategy Bonus

> **Status:** Active as part of the Unity-first assignment path; older
> standalone workflows in this folder are legacy reference.
>
> **Unity-first role:** Choose targets and retreat behavior for teammate
> automation.
>
> **Legacy-only:** PPO training is optional stretch work, not required for the
> core assignment.
>
> **Mini-test:** `ctest --preset linux-debug -R hw7`
>
> **Mini-test files:**
> - `HW7_strategy/tests/public/test_bt_semantics.cpp`
> - `HW7_strategy/tests/public/test_priority_distance.cpp`
> - `HW7_strategy/tests/public/test_retreat_trigger.cpp`

> 第七道作业（加分项）：给机器人写一个会做战术决策的大脑。
> 用一个极简的 Behavior Tree DSL 描述决策逻辑，C++ 编译它来跑。
> 两个 TODO（C++）：选目标 + 撤退判断。可选附加挑战：用 PPO 训
> 一个小策略网络替代 BT。
>
> **Your job (bonus):** finish two short C++ functions —
> `pick_target` (highest-priority enemy track) and `should_retreat`
> (HP/ammo thresholds + optional layer rules). The behaviour-tree
> runtime, the four leaf actions (engage/retreat/patrol/reload), and
> the YAML→C++ codegen are filled. The optional sub-skill is to
> train a PPO policy that replaces the BT — scaffold in
> `src/train_ppo.py`.

---

## Student Quickstart

### Prerequisites

- Complete the root [First-time setup](../README.md#first-time-setup).
- Use the Docker toolchain for C++ mini-tests.
- Optional PPO work needs `uv sync --group hw7`.

### What to implement

Fill the `TODO(HW7):` sites in:

- `HW7_strategy/include/aiming_hw/strategy/behavior_tree.hpp`
- `HW7_strategy/source/strategy.cpp`

Start with behavior-tree tick semantics if those tests are skipping, then fill
target selection and retreat logic.

### Mini-test command

```bash
cmake --preset linux-debug
cmake --build --preset linux-debug
ctest --preset linux-debug -R hw7
```

### Expected first run

Some tests can `GTEST_SKIP` while the strategy and behavior-tree blanks are
unfilled. Each completed `TODO(HW7):` block should convert its related skips to
passes.

### Before moving on

Run `ctest --preset linux-debug -R hw7`. Since HW7 is a bonus stage, make sure
A1-A6 mini-tests are already passing before spending time on PPO.

## 设计 / Design

```
            +----------------+
            |   Selector     |   "root"
            +-------+--------+
                    |
        +-----------+-----------+--------+
        |                       |        |
    Sequence              Sequence    Action
    "low_hp_branch"       "low_ammo"   "engage_or_patrol"
        |                       |
   should_retreat?         reload?
        |
   retreat_to_cover
```

* `behavior_tree.hpp` ships Sequence / Selector / Action and a typed
  Blackboard. No Parallel / Decorator nodes — HW7's tactical surface
  doesn't need them.
* `leaf_actions.hpp` declares the four leaves: `engage`,
  `retreat_to_cover`, `patrol`, `reload`. Each reads the runner's
  perception state from the Blackboard and writes an outcome flag.
* `strategy.hpp` exposes the two TODO targets the candidate fills:
  `pick_target` and `should_retreat`. Both take a `SelfInfo` + a
  `std::vector<TrackInfo>` so the public tests can drive them
  without instantiating the BT.
* `configs/example_bt.yaml` — sample BT spec. `src/dsl_to_cpp.py`
  reads it and emits a `.cpp` file with a `build_tree()` entry
  point HW6's runner calls during episode init.

---

## 你要写什么 / What you write

### 1. `pick_target(self, tracks)` (`source/strategy.cpp`)

Score every track and return the index of the highest-scoring
**enemy** (`is_ally == false`). Recommended scoring: weighted sum of
distance, estimated HP, and a close-range bonus. The public test
pins `closest enemy wins at equal HP` as the floor — your impl can
layer extra logic on top, but that rule must still hold.

### 2. `should_retreat(self, tracks)` (`source/strategy.cpp`)

Decide whether the BT should switch from engage to retreat. Floors
pinned by tests:

* `self.hp   <= 30` → return true
* `self.ammo <= 20` → return true

Optional layered rules: outnumbered + low HP, enemy within 1.5 m +
mid HP. The candidate's PPO sub-skill can replace this rule
entirely with a learned policy.

---

## 跑测试 / Running the tests

```bash
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug -R hw7"
```

Two GTest binaries — `hw7_priority_distance_test` (4 cases on
pick_target) and `hw7_retreat_trigger_test` (4 cases on
should_retreat). Each detects the unfilled-TODO state via a
sentinel call and `GTEST_SKIP`s with a clear message.

---

## DSL → C++ 代码生成 / Codegen

```bash
uv run python HW7_strategy/src/dsl_to_cpp.py \
    HW7_strategy/configs/example_bt.yaml \
    --out HW7_strategy/source/generated_bt.cpp
```

The generated file declares one symbol — `build_tree()` — that
HW6's runner instantiates during episode init. Three node kinds
supported: sequence, selector, action; six leaf names recognised
(`engage`, `retreat_to_cover`, `patrol`, `reload`,
`should_retreat_check`, `engage_or_patrol`).

---

## 可选附加 / Optional sub-skill

Train your own policy with PPO:

```bash
uv sync --group hw7
uv run python HW7_strategy/src/train_ppo.py \
    --episodes 1000 \
    --out /tmp/hw7_strategy.pt
```

The scaffold uses a stub env that emits canned observations; the
candidate's first task in this sub-skill is to swap in a real
gRPC-backed env that talks to the Unity arena. Stretch goal:
plug in [`sample-factory`](https://github.com/alex-petrenko/sample-factory)
for parallel rollouts (manual `pip install sample-factory` — kept
out of the declared deps because of platform finickiness).

---

## 已知非范围 / Out of scope

* Multi-agent communication beyond the simple ally-NPC channel
  (HW6 commands one ally over a second gRPC stream; HW7 only
  decides what to tell it, not how to coordinate).
* Full game-theoretic equilibrium analysis.
* The `gold` opponent policy itself — that's a team-side training
  job; HW7 candidates pull it via fetch_assets and play against it.
* Hidden grading episodes — grading must be redesigned from `schema.md`.
