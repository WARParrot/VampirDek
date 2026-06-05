using Core;
using Definitions;
using UnityEngine;

namespace Combat
{
    public static class SideLookup
    {
        public static SideState FindSideOf(IGameEntity entity)
        {
            var dm = Object.FindObjectOfType<DuelManager>();
            return FindSideOf(entity, dm?.CurrentDuelState);
        }

        public static SideState FindSideOf(IGameEntity entity, DuelState state)
        {
            if (state == null || entity == null) return null;
            if (Contains(state.PlayerSide, entity)) return state.PlayerSide;
            if (Contains(state.OpponentSide, entity)) return state.OpponentSide;
            return null;
        }

        public static SideState OpposingSide(SideState side, DuelState state)
        {
            if (state == null || side == null) return null;
            return side == state.PlayerSide ? state.OpponentSide : state.PlayerSide;
        }

        private static bool Contains(SideState side, IGameEntity entity)
        {
            foreach (var slot in side.Board.AllSlots())
                if (slot.Occupant == entity) return true;
            return false;
        }
    }
}
