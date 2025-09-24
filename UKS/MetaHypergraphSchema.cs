//
// PROPRIETARY AND CONFIDENTIAL
// Brain Simulator 3 v.1.0
// © 2022 FutureAI, Inc., all rights reserved
//

namespace UKS;

/// <summary>
/// Provides predefined schema elements for meta-hypergraph functionality.
/// These define standard node types and relationship types for enhanced reasoning.
/// </summary>
public static class MetaHypergraphSchema
{
    private static UKS? _uks;
    
    /// <summary>
    /// Sets the UKS instance to use for schema operations
    /// </summary>
    public static void SetUKS(UKS uks)
    {
        _uks = uks;
    }
    
    // Node types (Thing categories)
    public static Thing ConceptNode => _uks?.GetOrAddThing("concept-node", "NodeType") ?? ThingLabels.GetThing("concept-node");
    public static Thing ExemplarNode => _uks?.GetOrAddThing("exemplar-node", "NodeType") ?? ThingLabels.GetThing("exemplar-node");
    public static Thing SensorNode => _uks?.GetOrAddThing("sensor-node", "NodeType") ?? ThingLabels.GetThing("sensor-node");
    public static Thing AttributeNode => _uks?.GetOrAddThing("attribute-node", "NodeType") ?? ThingLabels.GetThing("attribute-node");
    public static Thing ActionNode => _uks?.GetOrAddThing("action-node", "NodeType") ?? ThingLabels.GetThing("action-node");
    
    // Hyperedge types (Relationship categories)
    public static Thing ClassMembership => _uks?.GetOrAddThing("class-membership", "RelationType") ?? ThingLabels.GetThing("class-membership");
    public static Thing HasAttribute => _uks?.GetOrAddThing("has-attribute", "RelationType") ?? ThingLabels.GetThing("has-attribute");
    public static Thing Exception => _uks?.GetOrAddThing("exception", "RelationType") ?? ThingLabels.GetThing("exception");
    public static Thing Conditional => _uks?.GetOrAddThing("conditional", "RelationType") ?? ThingLabels.GetThing("conditional");
    public static Thing Provenance => _uks?.GetOrAddThing("provenance", "RelationType") ?? ThingLabels.GetThing("provenance");
    public static Thing TemporalConstraint => _uks?.GetOrAddThing("temporal-constraint", "RelationType") ?? ThingLabels.GetThing("temporal-constraint");
    
    // Meta-relationship types
    public static Thing Implies => _uks?.GetOrAddThing("implies", "MetaRelationType") ?? ThingLabels.GetThing("implies");
    public static Thing Conflicts => _uks?.GetOrAddThing("conflicts", "MetaRelationType") ?? ThingLabels.GetThing("conflicts");
    public static Thing RequiredBy => _uks?.GetOrAddThing("required-by", "MetaRelationType") ?? ThingLabels.GetThing("required-by");
    
    /// <summary>
    /// Initializes the meta-hypergraph schema in the UKS
    /// </summary>
    public static void Initialize(UKS uks)
    {
        _uks = uks;
        
        // Ensure all schema elements exist in the UKS
        var conceptNode = uks.GetOrAddThing("concept-node", "NodeType");
        var exemplarNode = uks.GetOrAddThing("exemplar-node", "NodeType");
        var sensorNode = uks.GetOrAddThing("sensor-node", "NodeType");
        var attributeNode = uks.GetOrAddThing("attribute-node", "NodeType");
        var actionNode = uks.GetOrAddThing("action-node", "NodeType");
        
        var classMembership = uks.GetOrAddThing("class-membership", "RelationType");
        var hasAttribute = uks.GetOrAddThing("has-attribute", "RelationType");
        var exception = uks.GetOrAddThing("exception", "RelationType");
        var conditional = uks.GetOrAddThing("conditional", "RelationType");
        var provenance = uks.GetOrAddThing("provenance", "RelationType");
        var temporalConstraint = uks.GetOrAddThing("temporal-constraint", "RelationType");
        
        var implies = uks.GetOrAddThing("implies", "MetaRelationType");
        var conflicts = uks.GetOrAddThing("conflicts", "MetaRelationType");
        var requiredBy = uks.GetOrAddThing("required-by", "MetaRelationType");
    }
}
