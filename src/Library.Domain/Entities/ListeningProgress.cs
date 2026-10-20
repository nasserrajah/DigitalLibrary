using Library.Domain.Common;

namespace Library.Domain.Entities;

public class ListeningProgress : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid BookId { get; set; }
    public Book? Book { get; set; }

    /// <summary>فهرس آخر مقطع تم الاستماع إليه.</summary>
    public int LastSegmentIndex { get; set; }

    /// <summary>الموضع داخل المقطع بالثواني (للاستئناف الدقيق).</summary>
    public double LastPositionSeconds { get; set; }
}