using System;

namespace VoidFall.Core
{
    public enum CombatEncounterKind { Crossing, Volley, Pursuit, Flank, Hunt, Breakthrough }
    public enum CombatEncounterPhase { Flow, Warning, Deployment, ActiveThreat, Resolution, Recovery }

    public sealed class CombatEncounterClock
    {
        public CombatEncounterKind Kind { get; private set; }
        public CombatEncounterPhase Phase { get; private set; }
        public bool NeedsWithdrawal { get; private set; }
        public bool TimedOut { get; private set; }
        public int Admitted { get; private set; }
        public double PhaseSeconds { get; private set; }
        private double _recoverySeconds;
        private bool _sustained;

        public void BeginSustained(CombatEncounterKind kind, double recoverySeconds = 5)
        {
            Begin(kind, recoverySeconds);
            _sustained = true;
        }

        public void Begin(CombatEncounterKind kind, double recoverySeconds)
        {
            Reset(); Kind = kind;
            _recoverySeconds = double.IsNaN(recoverySeconds) || double.IsInfinity(recoverySeconds)
                ? 6 : Math.Max(0, recoverySeconds);
            Enter(CombatEncounterPhase.Warning);
        }

        public void CommitDeployment(int admitted)
        {
            if (Phase != CombatEncounterPhase.Deployment) return;
            Admitted = Math.Max(0, admitted);
            Enter(Admitted == 0 ? CombatEncounterPhase.Recovery : CombatEncounterPhase.ActiveThreat);
        }

        public void Step(double dt, int liveMembers, bool incomingThreat, bool safeOpening)
        {
            if (dt <= 0 || double.IsNaN(dt) || double.IsInfinity(dt) || Phase == CombatEncounterPhase.Flow) return;
            PhaseSeconds += dt;
            if (_sustained && Phase == CombatEncounterPhase.ActiveThreat)
            {
                // A change of pressure, not an order to delete surviving actors.
                if (PhaseSeconds >= 14 || (PhaseSeconds >= 3 && liveMembers == 0 && !incomingThreat))
                    Enter(CombatEncounterPhase.Recovery);
                return;
            }
            switch (Phase)
            {
                case CombatEncounterPhase.Warning:
                    if (PhaseSeconds + 1e-9 >= (_sustained ? .75 : 2.5)) Enter(CombatEncounterPhase.Deployment);
                    break;
                case CombatEncounterPhase.ActiveThreat:
                    if (liveMembers <= 0 && !incomingThreat) Enter(CombatEncounterPhase.Resolution);
                    else if (PhaseSeconds >= 18)
                    {
                        NeedsWithdrawal = TimedOut = true;
                        Enter(CombatEncounterPhase.Resolution);
                    }
                    break;
                case CombatEncounterPhase.Resolution:
                    if (liveMembers <= 0 && !incomingThreat && safeOpening)
                    {
                        NeedsWithdrawal = false;
                        Enter(CombatEncounterPhase.Recovery);
                    }
                    else if (PhaseSeconds >= 6) NeedsWithdrawal = true;
                    break;
                case CombatEncounterPhase.Recovery:
                    if (PhaseSeconds + 1e-9 >= _recoverySeconds) Enter(CombatEncounterPhase.Flow);
                    break;
            }
        }

        public void Reset()
        {
            Kind = default;
            Phase = CombatEncounterPhase.Flow;
            PhaseSeconds = 0;
            NeedsWithdrawal = TimedOut = false;
            Admitted = 0;
            _sustained = false;
        }

        private void Enter(CombatEncounterPhase phase)
        {
            Phase = phase;
            PhaseSeconds = 0;
        }
    }
}
