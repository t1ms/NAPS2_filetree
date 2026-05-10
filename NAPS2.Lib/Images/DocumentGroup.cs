using System;
using System.Collections.Generic;

namespace NAPS2.Images;

/// <summary>
/// Represents a logical grouping of scanned images (a "Document").
/// </summary>
public class DocumentGroup
{
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// The index field / name of this document. Used for file naming.
    /// </summary>
    public string IndexField { get; set; } = string.Empty;

    public DocumentGroup()
    {
    }

    public DocumentGroup(string initialIndexField)
    {
        IndexField = initialIndexField;
    }
}
