using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong
{
    [CreateAssetMenu(menuName = "Landsong/Quest/Quest Definition", fileName = "QuestDefinition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [SerializeField, InlineProperty, HideLabel]
        private GameQuestDefinition definition = new GameQuestDefinition();

        public GameQuestDefinition Data => definition;
        public string QuestId => definition == null ? string.Empty : definition.QuestId;
        public QuestCategory Category => definition == null ? QuestCategory.Mainline : definition.Category;
        public bool IsValid => definition != null && definition.IsValid;

        private void OnValidate()
        {
            definition ??= new GameQuestDefinition();
            definition.Normalize();
        }
    }
}
