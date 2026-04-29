#pragma once

// Tiny behaviour-tree runtime. Three node types — Sequence, Selector,
// Action — and a Blackboard that the leaf actions read/write. No
// reactive control-flow nodes (Parallel, Decorator) — HW7's tactical
// surface doesn't need them, and keeping the DSL small means the
// dsl_to_cpp.py codegen is ~80 lines.
//
// The runtime itself is filled. The two TODO(HW7) sites live in
// `strategy.cpp` (target prioritisation + retreat trigger) and are
// invoked by leaf actions.

#include <functional>
#include <memory>
#include <string>
#include <unordered_map>
#include <utility>
#include <variant>
#include <vector>

namespace aiming_hw {
namespace strategy {

enum class Status {
    Success,
    Failure,
    Running,
};

class Blackboard {
public:
    using Value = std::variant<bool, int, double, std::string>;

    template <typename T>
    void set(const std::string& key, T value) {
        values_[key] = Value(std::move(value));
    }

    template <typename T>
    T get(const std::string& key, T default_value = T{}) const {
        auto it = values_.find(key);
        if (it == values_.end()) return default_value;
        if (auto* v = std::get_if<T>(&it->second)) return *v;
        return default_value;
    }

    bool has(const std::string& key) const {
        return values_.find(key) != values_.end();
    }

    void clear() { values_.clear(); }

private:
    std::unordered_map<std::string, Value> values_;
};

class Node {
public:
    virtual ~Node() = default;
    virtual Status tick(Blackboard& bb) = 0;
    virtual std::string name() const = 0;
};

using NodePtr = std::unique_ptr<Node>;

// Action: leaf node — wraps a callable. Returning Status::Running keeps
// the BT in this branch on the next tick (a simple memoryless model).
class Action : public Node {
public:
    using Fn = std::function<Status(Blackboard&)>;

    Action(std::string label, Fn fn)
        : label_(std::move(label)), fn_(std::move(fn)) {}

    Status tick(Blackboard& bb) override {
        return fn_ ? fn_(bb) : Status::Failure;
    }
    std::string name() const override { return label_; }

private:
    std::string label_;
    Fn          fn_;
};

// Sequence: tick children left-to-right. First Failure stops and
// propagates; first Running stops and propagates; if all succeed
// returns Success.
class Sequence : public Node {
public:
    explicit Sequence(std::string label) : label_(std::move(label)) {}

    void add(NodePtr child) { children_.push_back(std::move(child)); }

    Status tick(Blackboard& bb) override {
        for (auto& c : children_) {
            const Status s = c->tick(bb);
            if (s != Status::Success) return s;
        }
        return Status::Success;
    }
    std::string name() const override { return label_; }

private:
    std::string          label_;
    std::vector<NodePtr> children_;
};

// Selector (Fallback): tick children left-to-right. First Success
// stops and propagates; first Running stops and propagates; if all
// fail returns Failure.
class Selector : public Node {
public:
    explicit Selector(std::string label) : label_(std::move(label)) {}

    void add(NodePtr child) { children_.push_back(std::move(child)); }

    Status tick(Blackboard& bb) override {
        for (auto& c : children_) {
            const Status s = c->tick(bb);
            if (s != Status::Failure) return s;
        }
        return Status::Failure;
    }
    std::string name() const override { return label_; }

private:
    std::string          label_;
    std::vector<NodePtr> children_;
};

// Convenience factories so the dsl_to_cpp.py codegen can emit
// readable C++ instead of raw `std::make_unique<Sequence>("...")`.
inline NodePtr action(std::string label, Action::Fn fn) {
    return std::make_unique<Action>(std::move(label), std::move(fn));
}

inline std::unique_ptr<Sequence> sequence(std::string label) {
    return std::make_unique<Sequence>(std::move(label));
}

inline std::unique_ptr<Selector> selector(std::string label) {
    return std::make_unique<Selector>(std::move(label));
}

}  // namespace strategy
}  // namespace aiming_hw
