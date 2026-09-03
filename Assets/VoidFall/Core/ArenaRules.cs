using System;

namespace VoidFall.Core
{

public enum ArenaPhase
{
    Idle,
    Warning,
    Collapse,
    Settle,
}

public enum ArenaTransitionEvent
{
    None,
    Warn,
    Swap,
    Complete,
    Deferred,
}

public readonly struct ArenaTransitionState
{
    public ArenaTransitionState(int index, double dueAt, ArenaPhase phase, double phaseT, ArenaId? incoming)
    {
        Index = index;
        DueAt = dueAt;
        Phase = phase;
        PhaseT = phaseT;
        Incoming = incoming;
    }

    public int Index { get; }
    public double DueAt { get; }
    public ArenaPhase Phase { get; }
    public double PhaseT { get; }
    public ArenaId? Incoming { get; }
}

public readonly struct ArenaStepResult
{
    public ArenaStepResult(ArenaTransitionState state, ArenaTransitionEvent transitionEvent)
    {
        State = state;
        Event = transitionEvent;
    }

    public ArenaTransitionState State { get; }
    public ArenaTransitionEvent Event { get; }
}

public static class ArenaRules
{
    public const int MinIntervalSeconds = 600;
    public const int MaxIntervalSeconds = 900;
    public const double WarningSeconds = 6;
    public const double CollapseSeconds = 0.72;
    public const double SettleSeconds = 1.1;
    public const double DeferRetrySeconds = 12;

    public static int ArenaIntervalSeconds(uint seed, int index)
    {
        var safeIndex = Math.Max(0, index);
        const uint span = MaxIntervalSeconds - MinIntervalSeconds + 1;
        return MinIntervalSeconds + (int)(ArenaHash(seed, safeIndex, 0x27d4eb2fu) % span);
    }

    public static double ArenaScheduleAt(uint seed, int index)
    {
        var safeIndex = Math.Max(0, index);
        var at = 0;
        for (var cursor = 0; cursor <= safeIndex; cursor++)
        {
            at += ArenaIntervalSeconds(seed, cursor);
        }

        return at;
    }

    public static ArenaId NextArenaId(uint seed, int index, ArenaId current)
    {
        var candidates = new ArenaId[ContentOrder.Arenas.Length - 1];
        var cursor = 0;
        foreach (var arena in ContentOrder.Arenas)
        {
            if (arena != current)
            {
                if (cursor >= candidates.Length) return ContentOrder.Arenas[0];
                candidates[cursor++] = arena;
            }
        }

        if (candidates.Length == 0) return current;
        var roll = ArenaHash(seed, Math.Max(0, index), 0x165667b1u) % (uint)candidates.Length;
        return candidates[roll];
    }

    public static ArenaTransitionState CreateTransitionState(uint seed)
    {
        return new ArenaTransitionState(0, ArenaIntervalSeconds(seed, 0), ArenaPhase.Idle, 0, null);
    }

    public static ArenaStepResult Step(
        ArenaTransitionState state,
        double dt,
        double time,
        uint seed,
        ArenaId current,
        bool blocked)
    {
        var step = !double.IsNaN(dt) && !double.IsInfinity(dt) ? Math.Max(0, dt) : 0;

        if (state.Phase == ArenaPhase.Idle)
        {
            if (time < state.DueAt) return new ArenaStepResult(state, ArenaTransitionEvent.None);
            if (blocked)
            {
                return new ArenaStepResult(
                    new ArenaTransitionState(state.Index, time + DeferRetrySeconds, ArenaPhase.Idle, 0, null),
                    ArenaTransitionEvent.Deferred);
            }

            return new ArenaStepResult(
                new ArenaTransitionState(state.Index, state.DueAt, ArenaPhase.Warning, WarningSeconds, NextArenaId(seed, state.Index, current)),
                ArenaTransitionEvent.Warn);
        }

        if (state.Phase == ArenaPhase.Warning)
        {
            if (blocked)
            {
                return new ArenaStepResult(
                    new ArenaTransitionState(state.Index, time + DeferRetrySeconds, ArenaPhase.Idle, 0, null),
                    ArenaTransitionEvent.Deferred);
            }

            var phaseT = state.PhaseT - step;
            if (phaseT > 0)
            {
                return new ArenaStepResult(
                    new ArenaTransitionState(state.Index, state.DueAt, ArenaPhase.Warning, phaseT, state.Incoming),
                    ArenaTransitionEvent.None);
            }

            return new ArenaStepResult(
                new ArenaTransitionState(state.Index, state.DueAt, ArenaPhase.Collapse, CollapseSeconds, state.Incoming),
                ArenaTransitionEvent.None);
        }

        if (state.Phase == ArenaPhase.Collapse)
        {
            var phaseT = state.PhaseT - step;
            if (phaseT > 0)
            {
                return new ArenaStepResult(
                    new ArenaTransitionState(state.Index, state.DueAt, ArenaPhase.Collapse, phaseT, state.Incoming),
                    ArenaTransitionEvent.None);
            }

            return new ArenaStepResult(
                new ArenaTransitionState(state.Index, state.DueAt, ArenaPhase.Settle, SettleSeconds, state.Incoming),
                ArenaTransitionEvent.Swap);
        }

        var settleT = state.PhaseT - step;
        if (settleT > 0)
        {
            return new ArenaStepResult(
                new ArenaTransitionState(state.Index, state.DueAt, ArenaPhase.Settle, settleT, state.Incoming),
                ArenaTransitionEvent.None);
        }

        var nextIndex = state.Index + 1;
        return new ArenaStepResult(
            new ArenaTransitionState(nextIndex, time + ArenaIntervalSeconds(seed, nextIndex), ArenaPhase.Idle, 0, null),
            ArenaTransitionEvent.Complete);
    }

    private static uint ArenaHash(uint seed, int index, uint salt)
    {
        unchecked
        {
            var value = seed ^ ((uint)(index + 1) * 0x9e3779b9u) ^ (salt * 0x85ebca6bu);
            value = (value ^ (value >> 16)) * 0x7feb352du;
            value = (value ^ (value >> 15)) * 0x846ca68bu;
            return value ^ (value >> 16);
        }
    }
}
}
