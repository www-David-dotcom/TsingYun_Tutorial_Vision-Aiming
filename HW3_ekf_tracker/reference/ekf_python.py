"""Reference EKF + IMM implementation.

This module is the **mathematical spec** the C++ skeleton in
HW3_ekf_tracker/source/ implements. It is also the numerical oracle
the public tests compare against (within 1e-9 in
test_cv_predict.cpp's golden values).

Read order:
1. State definition + measurement model — top of this file.
2. Motion models (cv_step / ct_step).
3. EKF predict + update - the C++ candidate blank mirrors these.
4. IMM mixing + per-mode update + state combination.
5. Hungarian assignment (here we delegate to scipy; the candidate
   writes it from scratch in C++ to keep the dep footprint small).

Convention (matches the C++ side):
    State vector: x = [px, py, vx, vy]  in world coordinates, meters / m·s⁻¹.
    Measurement:  z = [px, py]          (the camera-projected detection).
    Time step:    dt in seconds.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
from scipy.optimize import linear_sum_assignment

STATE_DIM = 4
MEAS_DIM = 2

H = np.array([[1.0, 0.0, 0.0, 0.0],
              [0.0, 1.0, 0.0, 0.0]])


# -------------------------------------------------------------- motion models

def cv_F(dt: float) -> np.ndarray:
    """Constant-velocity transition matrix."""
    F = np.eye(STATE_DIM)
    F[0, 2] = dt
    F[1, 3] = dt
    return F


def ct_F(dt: float, omega: float) -> np.ndarray:
    """Constant-turn transition matrix with fixed angular velocity ω.

    For ω → 0 this reduces to cv_F (we Taylor-expand around 0 to
    avoid division by zero).
    """
    if abs(omega) < 1e-6:
        return cv_F(dt)
    s = np.sin(omega * dt)
    c = np.cos(omega * dt)
    F = np.array([
        [1.0, 0.0,  s / omega,         -(1.0 - c) / omega],
        [0.0, 1.0,  (1.0 - c) / omega,  s / omega],
        [0.0, 0.0,  c,                 -s],
        [0.0, 0.0,  s,                  c],
    ])
    return F


def process_noise_cv(dt: float, sigma_a: float) -> np.ndarray:
    """Discretized white-noise acceleration for CV. sigma_a in m/s²."""
    q = sigma_a ** 2
    G = np.array([[0.5 * dt ** 2, 0.0],
                  [0.0, 0.5 * dt ** 2],
                  [dt, 0.0],
                  [0.0, dt]])
    return G @ G.T * q


def process_noise_ct(dt: float, sigma_a: float) -> np.ndarray:
    """Approximate Q for the CT model — same as CV with a small extra
    on velocity to absorb modeling error in the curvilinear motion."""
    return process_noise_cv(dt, sigma_a) * 1.5


# ----------------------------------------------------------------- EKF kernel

@dataclass
class GaussianBelief:
    x: np.ndarray   # [STATE_DIM]
    P: np.ndarray   # [STATE_DIM, STATE_DIM]


def predict(belief: GaussianBelief, F: np.ndarray, Q: np.ndarray) -> GaussianBelief:
    return GaussianBelief(x=F @ belief.x, P=F @ belief.P @ F.T + Q)


def update(belief: GaussianBelief,
           z: np.ndarray,
           R: np.ndarray) -> tuple[GaussianBelief, np.ndarray, np.ndarray]:
    """Measurement update. Returns the posterior plus the innovation
    (y) and innovation covariance (S) so callers can compute IMM
    likelihoods without redoing the linear algebra.

    Joseph-form covariance update for numerical stability — the
    naive (I - K H) P form drifts to non-symmetric P after a few
    hundred steps of single-precision arithmetic.
    """
    y = z - H @ belief.x
    S = H @ belief.P @ H.T + R
    K = belief.P @ H.T @ np.linalg.inv(S)
    x_new = belief.x + K @ y
    I_KH = np.eye(STATE_DIM) - K @ H
    P_new = I_KH @ belief.P @ I_KH.T + K @ R @ K.T
    return GaussianBelief(x=x_new, P=P_new), y, S


def gaussian_likelihood(y: np.ndarray, S: np.ndarray) -> float:
    """Multivariate-normal pdf of the innovation. Used by IMM mode
    probability update."""
    d = y.shape[0]
    detS = np.linalg.det(S)
    if detS <= 0:
        return 1e-300
    norm = 1.0 / np.sqrt((2 * np.pi) ** d * detS)
    expo = float(-0.5 * y.T @ np.linalg.inv(S) @ y)
    return norm * np.exp(expo)


# --------------------------------------------------------------- IMM

class IMM:
    """Two-mode IMM — CV + CT. The mode transition matrix `pi`
    governs how mode probabilities flow between modes; `omega_ct`
    parameterises the CT model's turn rate.

    State convention: mode 0 = CV, mode 1 = CT.
    """

    def __init__(self,
                 pi: np.ndarray,
                 omega_ct: float = 4.0,
                 sigma_a_cv: float = 1.0,
                 sigma_a_ct: float = 2.5) -> None:
        self.pi = np.asarray(pi, dtype=float)
        self.omega_ct = omega_ct
        self.sigma_a_cv = sigma_a_cv
        self.sigma_a_ct = sigma_a_ct
        self.beliefs: list[GaussianBelief] = []
        self.mu = np.array([0.5, 0.5])    # mode probs

    def initialise(self, x0: np.ndarray, P0: np.ndarray) -> None:
        self.beliefs = [GaussianBelief(x=x0.copy(), P=P0.copy()),
                        GaussianBelief(x=x0.copy(), P=P0.copy())]
        self.mu = np.array([0.5, 0.5])

    def step(self, dt: float, z: np.ndarray, R: np.ndarray) -> GaussianBelief:
        n = len(self.beliefs)

        # -------- mixing step
        c = self.pi.T @ self.mu                       # [n]
        mu_cond = (self.pi * self.mu[:, None]) / c    # mu_{i|j} = pi_{ij} * mu_i / c_j
        mixed: list[GaussianBelief] = []
        for j in range(n):
            x_j = sum(mu_cond[i, j] * self.beliefs[i].x for i in range(n))
            P_j = np.zeros((STATE_DIM, STATE_DIM))
            for i in range(n):
                dx = self.beliefs[i].x - x_j
                P_j += mu_cond[i, j] * (self.beliefs[i].P + np.outer(dx, dx))
            mixed.append(GaussianBelief(x=x_j, P=P_j))

        # -------- per-mode predict + update
        F_cv = cv_F(dt)
        F_ct = ct_F(dt, self.omega_ct)
        Q_cv = process_noise_cv(dt, self.sigma_a_cv)
        Q_ct = process_noise_ct(dt, self.sigma_a_ct)
        new_beliefs: list[GaussianBelief] = []
        likelihoods = np.zeros(n)
        for j in range(n):
            F = F_cv if j == 0 else F_ct
            Q = Q_cv if j == 0 else Q_ct
            pred = predict(mixed[j], F, Q)
            posterior, y, S = update(pred, z, R)
            likelihoods[j] = gaussian_likelihood(y, S)
            new_beliefs.append(posterior)

        # -------- mode probability update
        mu_new = c * likelihoods
        s = mu_new.sum()
        if s <= 0:
            mu_new = np.ones(n) / n
        else:
            mu_new = mu_new / s
        self.beliefs = new_beliefs
        self.mu = mu_new

        # -------- combination for the output belief
        x_comb = sum(self.mu[j] * self.beliefs[j].x for j in range(n))
        P_comb = np.zeros((STATE_DIM, STATE_DIM))
        for j in range(n):
            dx = self.beliefs[j].x - x_comb
            P_comb += self.mu[j] * (self.beliefs[j].P + np.outer(dx, dx))
        return GaussianBelief(x=x_comb, P=P_comb)


# -------------------------------------------------------------- multi-target

def gating_threshold(meas_dim: int, alpha: float = 0.99) -> float:
    """χ²-distribution upper bound for an alpha-confidence gate. We
    hardcode the 99% / dof=2 value here so the C++ side doesn't need
    a chi-squared table; alpha=0.99, dof=2 → 9.21."""
    if meas_dim == 2 and abs(alpha - 0.99) < 1e-6:
        return 9.21
    raise ValueError("only meas_dim=2, alpha=0.99 hardcoded")


def hungarian_assign(cost: np.ndarray, gate: float) -> list[tuple[int, int]]:
    """Returns (track_idx, detection_idx) pairs whose cost is below
    `gate`. Uses scipy.optimize.linear_sum_assignment for the inner
    bipartite matching; gating filters out the over-threshold
    pairings post-hoc."""
    if cost.size == 0:
        return []
    rows, cols = linear_sum_assignment(cost)
    return [(int(r), int(c)) for r, c in zip(rows, cols, strict=False) if cost[r, c] <= gate]


def mahalanobis_cost(track_x: np.ndarray,
                     track_P: np.ndarray,
                     detection: np.ndarray,
                     R: np.ndarray) -> float:
    """One-track-one-detection Mahalanobis cost for the assignment
    matrix. Uses the same H as the EKF update."""
    y = detection - H @ track_x
    S = H @ track_P @ H.T + R
    return float(y.T @ np.linalg.inv(S) @ y)
