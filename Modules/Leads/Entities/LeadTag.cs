namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class LeadTag
    {
        public Guid Id { get; set; }

        public int BusinessId { get; set; }

        public string Name { get; set; } = default!;
        public string? Color { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public ICollection<LeadTagMap> LeadTagMaps { get; set; } = new List<LeadTagMap>();
    }
}
