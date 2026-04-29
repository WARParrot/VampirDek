using UnityEngine;
using Core;
using Definitions;

namespace Combat
{
    public struct DamageDealtEvent(IGameEntity t, int a) : IGameEvent { public IGameEntity Target = t; public int Amount = a;
    }
    public struct DamagePreventedEvent(IGameEntity t, IGameEntity s) : IGameEvent { public IGameEntity Target = t; public IGameEntity Source = s;
    }
    public struct CardDrawnEvent(IPlayerSide side, ICard card) : IGameEvent { public IPlayerSide Side = side; public ICard Card = card;
    }
    public struct CardDiscardedEvent(IPlayerSide s, ICard c) : IGameEvent { public IPlayerSide Side = s; public ICard Card = c;
    }
    public struct ManaChangedEvent(IPlayerSide s) : IGameEvent { public IPlayerSide Side = s;
    }
    public struct PhaseEnterEvent(string id, List<string> tags) : IGameEvent { public string PhaseId = id; public List<string> Tags = tags;
    }
    public struct PhaseExitEvent(string id) : IGameEvent { public string PhaseId = id;
    }
    public struct ActionExecutedEvent(IGameAction a) : IGameEvent { public IGameAction Action = a;
    }
    public struct CardPlacedEvent(BoardCard c, Board b) : IGameEvent { public BoardCard Card = c; public Board Board = b;
    }
    public struct TownPlacedEvent(BoardCard l, Board b) : IGameEvent { public BoardCard Leader = l; public Board Board = b;
    }
    public struct EntityDiedEvent(IGameEntity e) : IGameEvent { public IGameEntity Entity = e;
    }
    public struct PlaceFailedEvent(string n, string r) : IGameEvent { public string CardName = n; public string Reason = r;
    }
    public struct PreDamageEvent(IGameEntity t, int a, IGameEntity s) : IGameEvent { public IGameEntity Target = t; public int Amount = a; public IGameEntity Source = s; public int ModifiedAmount = a; public bool Prevented = false;
    }
    public struct DuelPhaseTagEvent(string t) : IGameEvent { public string Tag = t;
    }
    public struct ClashResolvedEvent : IGameEvent { public BoardCard Winner; public BoardCard Loser; public ClashResolvedEvent(BoardCard w, BoardCard l) { Winner = w; Loser = l; }
    }
    public struct TownPlacedEvent : IGameEvent { public BoardCard Town; public Board Board; public TownPlacedEvent(BoardCard t, Board b) { Town = t; Board = b; }
    }
}
