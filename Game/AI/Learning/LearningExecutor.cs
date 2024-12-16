using System.Collections.Generic;
using System.IO;
using WindBot.Game.AI.Enums;

namespace WindBot.Game.AI.Learning
{
    public class LearningExecutor : DefaultExecutor
    {
        private ReinforcementLearning Learning;
        private List<ExecutorAction> PossibleActions;
        private ExecutorAction LastAction;

        public LearningExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            string savePath = Path.Combine(Program.AssetPath, "learning_data.json");
            Learning = new ReinforcementLearning(savePath);
            PossibleActions = new List<ExecutorAction>();
            LastAction = null;
        }

        protected override bool DefaultActivateCheck()
        {
            // Get current game state
            GameState state = GameState.FromDuel(Duel);
            
            // Collect possible actions
            PossibleActions.Clear();
            foreach (CardExecutor exec in Executors)
            {
                if (exec.Type == Type && exec.CardId == Card.Id)
                {
                    PossibleActions.Add(new ExecutorAction(exec.Type, exec.CardId, exec.Func));
                }
            }

            if (PossibleActions.Count == 0)
                return base.DefaultActivateCheck();

            // Let the learning system choose the action
            ExecutorAction chosenAction = Learning.ChooseAction(state, PossibleActions);
            LastAction = chosenAction;

            // Return true if we should activate
            return chosenAction.Type == Type && chosenAction.CardId == Card.Id;
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();

            // Observe the result of our last action
            GameState currentState = GameState.FromDuel(Duel);
            if (LastAction != null)
            {
                Learning.ObserveState(currentState, LastAction);
            }
        }

        public override void OnDuelEnd()
        {
            base.OnDuelEnd();

            // Final observation with the end state
            GameState finalState = GameState.FromDuel(Duel);
            if (LastAction != null)
            {
                Learning.ObserveState(finalState, LastAction, true);
            }
        }
    }
}
