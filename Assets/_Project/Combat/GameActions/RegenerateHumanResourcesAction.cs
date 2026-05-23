using System.Linq;
using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class RegenerateHumanResourcesAction : IGameAction
    {
        private SideState _side;
        public string Description => "Regenerate human resources";

        public RegenerateHumanResourcesAction(SideState side) => _side = side;

        public async UniTask ExecuteAsync()
        {
            _side.HumanResources = _side.Board.HumanRow.Count(s => !s.IsEmpty);
        }
    }
}
