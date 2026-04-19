using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace GitBashDesktop.Models
{
    public partial class ConflictFile : ObservableObject
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";

        public ObservableCollection<ConflictBlock> Blocks { get; } = new();

        public int TotalConflicts => Blocks.Count;
        public int ResolvedConflicts => Blocks.Count(b => b.IsResolved);
        public bool AllResolved => Blocks.Count > 0 &&
                                        Blocks.All(b => b.IsResolved);

        public bool IsDeleteModifyConflict { get; set; } = false;

        public string StatusText => IsDeleteModifyConflict
            ? "Delete/modify conflict"
            : $"{ResolvedConflicts}/{TotalConflicts} resolved";

        public string StatusColor => IsDeleteModifyConflict
            ? "#F47067"
            : AllResolved ? "#1D9E75" : "#E2B714";


    }
}