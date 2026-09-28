namespace Restaurant.Domain.Catalog;

/// <summary>
/// Routing target for order tickets. A data attribute on Product, not hardcoded
/// logic — adding a new station later is a config change, not a redesign
/// (docs/architecture/01-mvp-technical-design.md, section 1.3).
/// </summary>
public enum Station
{
    Kitchen,
    Bar
}
