namespace Definitions
{
    public interface IPreventableAction
    {
        bool IsPrevented { get; set; }
        void OnPrevention();
    }
}
