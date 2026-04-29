# 评分流程 / Grading workflow

> 本作业的评分采用**自动评分公开测试 + 人工面试**的两段制。
> 公开测试在 GitHub Actions 上跑，PR 一开就有反馈；最终录取由
> 队里在 PR 截止后人工 review + 面试 top-N 决定。
>
> **TL;DR (English):** Two-stage rubric. Public tests are graded
> automatically on every PR (CI on `ubuntu-latest`); top-ranked
> candidates are interviewed in person. Hidden tests and live-arena
> matches are deferred to a later cycle.

---

## 阶段 1：自动评分 / Stage 1: auto-graded public tests

**机制 / Mechanism**

每次你向[候选人仓库](https://github.com/www-David-dotcom/TsingYun_Tutorial_Vision-Aiming)
提 PR，`.github/workflows/validate_submission.yml` 会做以下事情：

1. Checkout 你 PR 的 `head.sha`（不是 main，所以你最新一次 push 的内容
   就是被测的内容）。
2. 安装 `cmake / ninja / g++-12 / libeigen3 / libgtest`，运行 `uv sync`。
3. `cmake --preset linux-debug && cmake --build --preset linux-debug` 构建
   所有可在 `ubuntu-latest` 上构建的 HW（HW2 / HW3 / HW4 / HW5 PID /
   HW6 / HW7 一定可以；HW1 ONNX 推理 / HW5 MPC 因为依赖 onnxruntime /
   acados，会被 CMake 的 skip-guard 跳过——这不算扣分）。
4. `ctest --preset linux-debug --output-junit ctest_results.xml` 跑 C++
   公开测试，`uv run pytest --junit-xml pytest_results.xml -v` 跑 Python
   公开测试。
5. `tools/leaderboard/score_pr.py` 解析 JUnit XML，按 HW 分组统计通过
   数，生成一条评论挂到你的 PR 上（包含每个 HW 的 passed / failed /
   skipped）。
6. 整套测试结果作为 `submission-score-<PR编号>` artefact 上传，保留 30
   天。

整个流程 5–10 分钟。你 push 一次就能看一次评分。

**评分依据 / Score**

* `score = 通过的公开测试数`。
* 跳过的测试**不算扣分**（HW1 / HW5 MPC 在 `ubuntu-latest` 上会跳过
  optional 依赖）。失败才扣分。
* 排序时，相同总数下，覆盖 HW 数更多的候选人靠前（HW2/HW3/HW4 都过
  3 题 > 只刷 HW2 单 9 题）。

**反作弊 / Anti-cheat**

CI 重新跑你 `head.sha` 上的所有测试——你不能在 PR description 里编造分
数。这是「君子协定 + 验证最低保证」（schema.md §7）。如果将来发现作
弊，团队会在面试环节当场重跑代码。

---

## 阶段 2：每日 leaderboard / Stage 2: daily leaderboard

`.github/workflows/regenerate_leaderboard.yml` 在北京时间每天 19:17 跑
（cron 17 11 \* \* \* UTC）。它会：

1. 用 `gh pr list` 列出所有 open PR；
2. 对每个 PR 拉最近一次 `validate_submission.yml` 的 artefact；
3. 解析 `submission_score.json`，按 「总分降序、HW 覆盖广度降序」排序；
4. 把结果写到一个 **orphan branch `leaderboard`**（候选人在做作业的时候
   看不到——main 里没有这个分支）：
   * `leaderboard/leaderboard.md` —— 给团队看的 Markdown 表格
   * `leaderboard/leaderboard.csv` —— 同样数据，可塞进表格软件
   * `leaderboard/leaderboard.json` —— 程序读取用
5. 如果当天没有数据变化，跳过 push（避免空提交）。

排行榜不公开。最终录取以人工 review + 面试为准；leaderboard 只是「给
team 看一眼今天有几个新提交」的运营工具。

---

## 候选人怎么提交 / Submission workflow

```bash
# 1. Fork 候选人仓库到自己的 GitHub 账号（界面上点 Fork）。
# 2. Clone 你 fork 的仓库：
git clone https://github.com/<你的用户名>/TsingYun_Tutorial_Vision-Aiming
cd TsingYun_Tutorial_Vision-Aiming

# 3. 创建分支、填 TODO、本地跑测试：
uv sync
cmake --preset linux-debug
cmake --build --preset linux-debug
ctest --preset linux-debug
uv run pytest

# 4. push 到自己 fork。
git push origin main

# 5. 在 GitHub 网页上向 www-David-dotcom/TsingYun_Tutorial_Vision-Aiming
#    的 main 分支开 PR。PR 标题改为「姓名 - 学号」，description 里贴
#    一段简短的功能展示 + 遇到的问题。
```

PR 一开 CI 就跑，5–10 分钟内你的 PR 上会出现一条 `## Aiming HW —
public test summary` 的评论。继续 push 时 CI 会重跑并**更新同一条评论**
（不会刷屏）。

---

## 时间线 / Timeline

* PR 随时可以开（`opened` 触发 CI）。
* 截止日期由团队公布；截止后两周内出录取结果。
* 截止前你可以无限次 push；CI 总是只看 `head.sha`，不看历史。

---

## 已知会跳过的测试 / Known-skipped tests on `ubuntu-latest`

| HW | 跳过的测试 | 跳过原因 |
|----|-----------|----------|
| HW1 | `test_loss_shapes.py`, `test_export_roundtrip.py` | 需要 `torch` + `torchvision`（`uv sync --group hw1`） |
| HW1 | `hw1_post_process_test` | 需要 ONNX Runtime（CMakeLists 自动 skip） |
| HW5 | MPC controller path | 需要 acados-codegened solver（团队侧产物） |
| HW3 | scipy-dependent fixture regen | 候选人不需要重跑，CSV 已经在 repo 里 |

这些**不影响公开评分**——score_pr.py 把 skipped 当 0（既不算 pass 也不
算 fail）。如果你在自己机器上跑全套 HW1 / HW5 通过，可以在 PR
description 里写一句，团队人工 review 时会留意。

---

## 故障排查 / Troubleshooting

* **CI 没跑 / 没评论**：检查 PR 是 draft 吗？draft PR 不会触发
  `validate submission`。点「Ready for review」即可。
* **CI 第一次失败但第二次成功**：GitHub-hosted runner 偶尔会 apt-get
  失败。push 一个空 commit `git commit --allow-empty -m "ci: retry"`
  即可重跑。
* **测试在我本地通过但 CI 失败**：本地 / CI 的 `g++` 版本不同（CI 用
  g++-12）。运行 `docker compose -f shared/docker/toolchain.compose.yaml
  run --rm dev bash` 进 toolchain 镜像，里面的环境就是 CI 的环境。

---

## 团队侧 ops / For the team

* 排行榜 cron：`17 11 * * *` UTC = 北京 19:17 每天一次。
  调整时间编辑 `.github/workflows/regenerate_leaderboard.yml`。
* 手动重跑 leaderboard：到 Actions tab → `regenerate leaderboard` →
  Run workflow。
* 拉所有候选人的最新 score 到本地：
  ```bash
  uv run python tools/leaderboard/aggregate.py \
      --repo www-David-dotcom/TsingYun_Tutorial_Vision-Aiming \
      --out-csv  /tmp/leaderboard.csv \
      --out-md   /tmp/leaderboard.md \
      --out-json /tmp/leaderboard.json
  ```
* 看一个候选人的失败详情：到 PR → Checks → `validate submission` →
  Artifacts → 下载 `submission-score-*.zip`，里面有 `ctest_results.xml`
  和 `pytest_results.xml`。

---

## 未来扩展 / Future cycles

Proposal A 是最低成本起步方案。下一轮可能会加：

* **隐藏测试** —— team 仓库里的 hidden grader（运行在团队机器，不在
  GitHub Actions），跑 HW1 mAP / HW3 NEES / HW6 端到端胜率等。
* **Live arena 评估** —— Orin NX 跑 5 局 vs silver / gold，胜率 + p95
  延迟入排行。
* **build-artefact 哈希绑定** —— 让 score JSON 同时含 build SHA 和
  seed-manifest hash，更难造假。

详情见 `IMPLEMENTATION_PLAN.md` Stage 10 v1 § "future cycles" 部分。
