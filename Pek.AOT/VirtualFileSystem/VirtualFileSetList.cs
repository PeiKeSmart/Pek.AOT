namespace Pek.VirtualFileSystem;

public class VirtualFileSetList : List<IVirtualFileSet>
{
    public List<String> PhysicalPaths { get; }

    public VirtualFileSetList()
    {
        PhysicalPaths = new List<String>();
    }
}
