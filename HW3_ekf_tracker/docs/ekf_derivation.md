# HW3 EKF + IMM derivation

This is the math reference for the four TODO sites in HW3. The C++
skeleton mirrors `reference/ekf_python.py` line-for-line; if anything
here disagrees with the Python file, the Python is the canon.

## State and measurement

Per-target state in 2D world coordinates:

```
x = [px, py, vx, vy]^T            (4×1)
```

Position-only measurement (the camera-projected detection):

```
z = [px, py]^T                    (2×1)
```

Linear measurement model:

```
H = [ 1 0 0 0 ]                   (2×4)
    [ 0 1 0 0 ]
```

Diagonal additive noise:

```
R = σ_z^2 · I_2                   (2×2),   σ_z ≈ 0.05 m
```

## Motion models

### Constant velocity (CV)

```
F_CV(dt) = I_4 + dt · ⎡0 0 1 0⎤   = ⎡1 0 dt  0 ⎤
                     ⎢0 0 0 1⎥     ⎢0 1  0 dt ⎥
                     ⎢0 0 0 0⎥     ⎢0 0  1  0 ⎥
                     ⎣0 0 0 0⎦     ⎣0 0  0  1 ⎦
```

### Constant turn (CT, fixed ω)

For ω ≠ 0:

```
F_CT(dt, ω) =
  ⎡1  0   sin(ωdt)/ω    -(1-cos(ωdt))/ω ⎤
  ⎢0  1   (1-cos(ωdt))/ω    sin(ωdt)/ω  ⎥
  ⎢0  0   cos(ωdt)        -sin(ωdt)     ⎥
  ⎣0  0   sin(ωdt)         cos(ωdt)     ⎦
```

For ω → 0 the matrix degenerates to F_CV via the standard
sin(x)/x → 1 limit. The C++ `ct_transition` Taylor-expands explicitly
when |ω| < 1e-6.

### Process noise

Discretised white-noise acceleration with stddev σ_a:

```
G(dt) = ⎡½dt² 0   ⎤      Q_CV(dt, σ_a) = G G^T · σ_a²
        ⎢0    ½dt²⎥
        ⎢dt   0   ⎥
        ⎣0    dt  ⎦
```

The CT mode uses 1.5 × Q_CV to absorb the residual from approximating
curvilinear motion with the linear F.

## Kalman primitives

Predict:

```
x' = F x
P' = F P F^T + Q
```

Update (Joseph form — required):

```
y  = z - H x
S  = H P H^T + R
K  = P H^T S^{-1}
x' = x + K y
P' = (I - K H) P (I - K H)^T + K R K^T
```

The naive form `P' = (I - K H) P` is mathematically equivalent but
loses symmetry to roundoff after a few hundred double-precision
steps. The 1800-step replay test in HW3's hidden suite catches the
asymmetry.

## IMM (two-mode CV + CT)

Mode index j ∈ {0 = CV, 1 = CT}. Mode probabilities μ_j ∈ [0, 1] sum
to 1. Mode transition matrix π:

```
π = ⎡0.95 0.05⎤    (default)
    ⎣0.10 0.90⎦
```

### Step 1: mixing

```
c_j     = Σ_i π_{ij} μ_i                              (normaliser, [n])
μ_{i|j} = π_{ij} μ_i / c_j                            (n × n)
x^j_mix = Σ_i μ_{i|j} x^i
P^j_mix = Σ_i μ_{i|j} (P^i + (x^i - x^j_mix)(x^i - x^j_mix)^T)
```

### Step 2: per-mode predict + update

Each mode runs the standard predict + Joseph update with **its** F
and Q, on the mixed prior. Returns posterior `(x^j, P^j)` and
likelihood `Λ^j = N(y^j ; 0, S^j)`.

### Step 3: mode probability update

```
μ_j_new ∝ c_j · Λ^j      then renormalise.
```

If every Λ^j collapses to zero (the gate rejected everything), reset
to a uniform prior so the tracker recovers gracefully.

### Step 4: combination

```
x_comb = Σ_j μ_j x^j
P_comb = Σ_j μ_j (P^j + (x^j - x_comb)(x^j - x_comb)^T)
```

This is the belief the public API publishes.

## Multi-target data association

For tracks `T_1..T_m` and detections `D_1..D_n`:

1. Build a m × n cost matrix where `cost[i][j] = (z_j - H x_i)^T S_i^{-1} (z_j - H x_i)`
   (squared Mahalanobis distance).
2. Run Hungarian (Kuhn–Munkres, square-padding) for the
   minimum-cost assignment.
3. Drop pairings whose cost exceeds the χ² gate (9.21 for dof=2 at
   the 99th percentile).
4. Step matched tracks with their assigned detection. Coast
   unmatched tracks (predict-only). Spawn new tracks from unmatched
   detections.
5. Drop tracks whose miss-streak exceeds `coast_max_steps`.

Cross-class behaviours (different icons co-locating at long range)
are handled by HW1's NMS in C++ before detections reach HW3, so the
EKF only sees deduplicated targets.

## Performance signal

* CV-mode predict matches an analytical step within 1e-12.
* IMM mode probabilities sum to 1 every step (∫|μ - 1|·dt < 1e-9
  over 1800 steps).
* Joseph form keeps |P - P^T| below 1e-9 after 1800 alternating
  predict + update steps with σ_z = 0.05 m.
* The straight-line trajectory drives μ_CV → > 0.7 within 200 steps;
  the constant-turn (ω = 4 rad/s) trajectory drives μ_CT → > 0.5
  within 240 steps.

These are PoC numbers, not the production NEES/RMSE bars. HW3's full
quality gate (95% NEES coverage, ≤ 0.10 m RMSE on `traj_med`) is
deferred until the grading workflow is designed in Stage 10.
