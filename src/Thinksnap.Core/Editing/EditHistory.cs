using Thinksnap.Core.Annotations;

namespace Thinksnap.Core.Editing;

public sealed class EditHistory
{
    private readonly List<AnnotationOperation> _operations = [];

    public IReadOnlyList<AnnotationOperation> Operations => _operations;

    public void Add(AnnotationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        _operations.Add(operation);
    }

    public AnnotationOperation? Undo()
    {
        if (_operations.Count == 0)
        {
            return null;
        }

        var index = _operations.Count - 1;
        var operation = _operations[index];
        _operations.RemoveAt(index);
        return operation;
    }
}
