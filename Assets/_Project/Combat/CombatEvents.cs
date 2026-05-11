using System.Collections.Generic;
using Core;
using Definitions;

namespace Combat
{
    public struct DamageDealtEvent : IGameEvent { public IGameEntity Target; public int Amount; public DamageDealtEvent(IGameEntity t, int a) { Target=t; Amount=a; }
    }
    public struct DamagePreventedEvent : IGameEvent { public IGameEntity Target; public IGameEntity Source; public DamagePreventedEvent(IGameEntity t, IGameEntity s) { Target=t; Source=s; } 
    }
    public struct CardDrawnEvent : IGameEvent { public IPlayerSide Side; public ICard Card; public CardDrawnEvent(IPlayerSide side, ICard card) { Side=side; Card=card; }
    }
    public struct CardDiscardedEvent : IGameEvent { public IPlayerSide Side; public ICard Card; public CardDiscardedEvent(IPlayerSide s, ICard c) { Side=s; Card=c; }
    }
    public struct ManaChangedEvent : IGameEvent { public IPlayerSide Side; public ManaChangedEvent(IPlayerSide s) { Side=s; }
    }
    public struct PhaseEnterEvent : IGameEvent { public string PhaseId; public List<string> Tags; public PhaseEnterEvent(string id, List<string> tags) { PhaseId=id; Tags=tags; }
    }
    public struct PhaseExitEvent : IGameEvent { public string PhaseId; public PhaseExitEvent(string id){ PhaseId=id; }
    }
    public struct ActionExecutedEvent : IGameEvent { public IGameAction Action; public ActionExecutedEvent(IGameAction a) { Action=a; }
    }
    public struct PlacedCardEvent : IGameEvent { public BoardCard Card; public Board Board; public PlacedCardEvent(BoardCard c, Board b) { Card=c; Board=b; }
    }
    public struct EntityDiedEvent : IGameEvent { public IGameEntity Entity; public EntityDiedEvent(IGameEntity e) { Entity=e; }
    }
    public struct PlaceFailedEvent : IGameEvent { public string CardName; public string Reason; public PlaceFailedEvent(string n, string r) { CardName=n; Reason=r; }
    }
    public struct PreDamageEvent : IGameEvent { public IGameEntity Target; public int Amount; public IGameEntity Source; public int ModifiedAmount; public bool Prevented; public PreDamageEvent(IGameEntity t, int a, IGameEntity s) { Target=t; Amount=a; Source=s; ModifiedAmount=a; Prevented=false; }
    }
    public struct DuelPhaseTagEvent : IGameEvent { public string Tag; public DuelPhaseTagEvent(string t) { Tag=t; }
    }
    public struct ClashResolvedEvent : IGameEvent { public BoardCard Winner; public BoardCard Loser; public ClashResolvedEvent(BoardCard w, BoardCard l) { Winner = w; Loser = l; }
    }
    public struct TownPlacedEvent : IGameEvent { public BoardCard Town; public Board Board; public TownPlacedEvent(BoardCard t, Board b) { Town = t; Board = b; }
    }
    public struct DuelEndedEvent : IGameEvent { }
}
