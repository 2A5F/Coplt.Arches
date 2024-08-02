using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Coplt.Arches;

public record struct ArcheTypeOptions()
{
    /// <summary>
    /// The Stride will be automatically calculated based on the PageSize and input type
    /// </summary>
    public int PageSize { get; set; } = 16 * 1024;
    /// <summary>
    /// A stride greater than 0 will ignore PageSize
    /// </summary>
    public int Stride { get; set; }
    /// <summary>
    /// Whether to split the managed type. If split, maximum of 2 <see cref="ArcheTypeUnitMeta"/> will be output.
    /// </summary>
    public bool SplitManaged { get; set; }
    /// <summary>
    /// Default is generate class.<br/>
    /// <para>
    /// The accessor needs to input the class, when generate structure,
    /// the class containing the structure needs to be passed in
    /// </para>
    /// </summary>
    public bool GenerateStructure { get; set; }
}
