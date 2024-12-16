using WindBot.Game.AI;

namespace WindBot.Game.AI.Learning
{
    public class ExecutorAction
    {
        public ExecutorType Type { get; set; }
        public int CardId { get; set; }
        public System.Func<bool> Condition { get; set; }
        public float PredictedValue { get; set; }

        public ExecutorAction(ExecutorType type, int cardId, System.Func<bool> condition)
        {
            Type = type;
            CardId = cardId;
            Condition = condition;
        }
    }
}
