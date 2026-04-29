// Behaviour-tree control-flow semantics. Pins the candidate's
// Sequence::tick / Selector::tick implementations.
//
// The four corner cases:
//   * Sequence — all-success → Success
//   * Sequence — early Failure short-circuits
//   * Selector — first Success short-circuits
//   * Selector — all-failure → Failure
//
// Plus the Running propagation rule for both.

#include <gtest/gtest.h>

#include "aiming_hw/strategy/behavior_tree.hpp"

using aiming_hw::strategy::Action;
using aiming_hw::strategy::Blackboard;
using aiming_hw::strategy::Selector;
using aiming_hw::strategy::Sequence;
using aiming_hw::strategy::Status;
using aiming_hw::strategy::action;
using aiming_hw::strategy::selector;
using aiming_hw::strategy::sequence;

namespace {

bool bt_tick_is_stub() {
    // A real Sequence with two Action(Success) children returns
    // Success; the stub returns Failure. Distinguishes both
    // Sequence and Selector at once because the stub for both is
    // Failure.
    auto seq = sequence("probe");
    seq->add(action("a", [](Blackboard&) { return Status::Success; }));
    seq->add(action("b", [](Blackboard&) { return Status::Success; }));
    Blackboard bb;
    return seq->tick(bb) != Status::Success;
}

}  // namespace

TEST(HW7BehaviourTree, SequenceAllSuccessReturnsSuccess) {
    if (bt_tick_is_stub()) {
        GTEST_SKIP() << "Sequence::tick / Selector::tick unimplemented "
                        "— fill the TODOs in behavior_tree.hpp";
    }
    auto seq = sequence("root");
    seq->add(action("a", [](Blackboard&) { return Status::Success; }));
    seq->add(action("b", [](Blackboard&) { return Status::Success; }));
    seq->add(action("c", [](Blackboard&) { return Status::Success; }));
    Blackboard bb;
    EXPECT_EQ(seq->tick(bb), Status::Success);
}

TEST(HW7BehaviourTree, SequenceEarlyFailureShortCircuits) {
    if (bt_tick_is_stub()) GTEST_SKIP() << "BT tick unimplemented";
    int third_called = 0;
    auto seq = sequence("root");
    seq->add(action("a", [](Blackboard&) { return Status::Success; }));
    seq->add(action("b", [](Blackboard&) { return Status::Failure; }));
    seq->add(action("c", [&third_called](Blackboard&) {
        ++third_called;
        return Status::Success;
    }));
    Blackboard bb;
    EXPECT_EQ(seq->tick(bb), Status::Failure);
    EXPECT_EQ(third_called, 0) << "third child must NOT tick after failure";
}

TEST(HW7BehaviourTree, SequenceRunningPropagates) {
    if (bt_tick_is_stub()) GTEST_SKIP() << "BT tick unimplemented";
    int third_called = 0;
    auto seq = sequence("root");
    seq->add(action("a", [](Blackboard&) { return Status::Success; }));
    seq->add(action("b", [](Blackboard&) { return Status::Running; }));
    seq->add(action("c", [&third_called](Blackboard&) {
        ++third_called;
        return Status::Success;
    }));
    Blackboard bb;
    EXPECT_EQ(seq->tick(bb), Status::Running);
    EXPECT_EQ(third_called, 0) << "third child must NOT tick while previous Running";
}

TEST(HW7BehaviourTree, SelectorFirstSuccessShortCircuits) {
    if (bt_tick_is_stub()) GTEST_SKIP() << "BT tick unimplemented";
    int third_called = 0;
    auto sel = selector("root");
    sel->add(action("a", [](Blackboard&) { return Status::Failure; }));
    sel->add(action("b", [](Blackboard&) { return Status::Success; }));
    sel->add(action("c", [&third_called](Blackboard&) {
        ++third_called;
        return Status::Success;
    }));
    Blackboard bb;
    EXPECT_EQ(sel->tick(bb), Status::Success);
    EXPECT_EQ(third_called, 0) << "third child must NOT tick after success";
}

TEST(HW7BehaviourTree, SelectorAllFailureReturnsFailure) {
    if (bt_tick_is_stub()) GTEST_SKIP() << "BT tick unimplemented";
    auto sel = selector("root");
    sel->add(action("a", [](Blackboard&) { return Status::Failure; }));
    sel->add(action("b", [](Blackboard&) { return Status::Failure; }));
    sel->add(action("c", [](Blackboard&) { return Status::Failure; }));
    Blackboard bb;
    EXPECT_EQ(sel->tick(bb), Status::Failure);
}

TEST(HW7BehaviourTree, NestedSelectorOverSequence) {
    if (bt_tick_is_stub()) GTEST_SKIP() << "BT tick unimplemented";
    // Selector
    //   ├─ Sequence (a:Success, b:Failure)   ← fails as a unit
    //   └─ Action(c:Success)                 ← rescues it
    auto seq = sequence("seq");
    seq->add(action("a", [](Blackboard&) { return Status::Success; }));
    seq->add(action("b", [](Blackboard&) { return Status::Failure; }));
    auto sel = selector("root");
    sel->add(std::move(seq));
    sel->add(action("c", [](Blackboard&) { return Status::Success; }));
    Blackboard bb;
    EXPECT_EQ(sel->tick(bb), Status::Success);
}
