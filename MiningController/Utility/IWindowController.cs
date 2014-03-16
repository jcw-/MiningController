
namespace MiningController
{
    public interface IWindowController
    {
        void ToggleWindowVisibilityByProcessName(string processName);

        void SetWindowVisibilityByProcessName(string processName, bool visible);

        bool IsWindowVisibleByProcessName(string processName);
    }
}
