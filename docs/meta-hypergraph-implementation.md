# Meta-Hypergraph Schema for BrainSim III UKS

## Theoretical Foundation
This document outlines a meta-hypergraph formalism that directly maps to BrainSim III's cognitive primitives, extending the current UKS architecture to support more sophisticated reasoning patterns while maintaining compatibility with existing Thing/Relationship structures.

## Core Extensions to UKS

### 1. Hyperedge Types (extends current Relationship system)

```csharp
public enum HyperedgeType 
{
    // Basic (existing)
    SimpleRelation,        // Current UKS Relationships
    
    // Meta-hypergraph extensions
    CompoundRelation,      // Links multiple Things simultaneously
    MetaRelation,          // Links other Relationships 
    ConditionalRelation,   // "IF-THEN" structures
    ExceptionRelation,     // Overrides inherited attributes
    ProvenanceRelation,    // Tracks source/confidence of facts
    TemporalRelation       // Time-bounded relationships
}
```

### 2. Enhanced Relationship Class

```csharp
public class MetaRelationship : Relationship
{
    public HyperedgeType HyperedgeType { get; set; } = HyperedgeType.SimpleRelation;
    
    // For compound relations (multiple targets)
    public List<Thing> Targets { get; set; } = new();
    
    // For meta-relations (relationship targets)
    public List<Relationship> RelationshipTargets { get; set; } = new();
    
    // Exception handling
    public float Priority { get; set; } = 1.0f; // Higher priority overrides
    public bool IsException { get; set; } = false;
    
    // Conditional logic
    public List<Relationship> Conditions { get; set; } = new(); // IF conditions
    public List<Relationship> Consequences { get; set; } = new(); // THEN consequences
    
    // Provenance
    public float Confidence { get; set; } = 1.0f;
    public string Source { get; set; } = "";
    public DateTime AcquiredAt { get; set; } = DateTime.Now;
}
```

### 3. Schema Definition

```csharp
public static class MetaHypergraphSchema
{
    // Node types (Thing categories)
    public static readonly Thing ConceptNode = "concept-node";
    public static readonly Thing ExemplarNode = "exemplar-node"; 
    public static readonly Thing SensorNode = "sensor-node";
    public static readonly Thing AttributeNode = "attribute-node";
    public static readonly Thing ActionNode = "action-node";
    
    // Hyperedge types (Relationship categories)
    public static readonly Thing ClassMembership = "class-membership";
    public static readonly Thing HasAttribute = "has-attribute";
    public static readonly Thing Exception = "exception";
    public static readonly Thing Conditional = "conditional";
    public static readonly Thing Provenance = "provenance";
    public static readonly Thing TemporalConstraint = "temporal-constraint";
    
    // Meta-relationship types
    public static readonly Thing Implies = "implies";
    public static readonly Thing Conflicts = "conflicts";
    public static readonly Thing RequiredBy = "required-by";
}
```

## Practical Example: "Dogs Play Outside If Sunny" + Tripper Exception

### Knowledge Encoding

```csharp
public class DogPlaygroundExample
{
    private UKS.UKS uks;
    
    public void EncodeKnowledge()
    {
        // Basic concepts
        Thing dog = uks.GetOrAddThing("dog", MetaHypergraphSchema.ConceptNode);
        Thing weather = uks.GetOrAddThing("weather", MetaHypergraphSchema.ConceptNode);
        Thing sunny = uks.GetOrAddThing("sunny", MetaHypergraphSchema.AttributeNode);
        Thing playOutside = uks.GetOrAddThing("play-outside", MetaHypergraphSchema.ActionNode);
        
        // Specific instances
        Thing fido = uks.CreateInstanceOf(dog, MetaHypergraphSchema.ExemplarNode);
        Thing tripper = uks.CreateInstanceOf(dog, MetaHypergraphSchema.ExemplarNode);
        
        // Base rule: "Dogs play outside if weather is sunny"
        var conditionalRel = new MetaRelationship
        {
            HyperedgeType = HyperedgeType.ConditionalRelation,
            source = dog,
            relType = MetaHypergraphSchema.Conditional,
            target = playOutside,
            Confidence = 0.8f,
            Source = "general-knowledge"
        };
        
        // Condition: weather is sunny
        var weatherCondition = new Relationship
        {
            source = weather,
            relType = MetaHypergraphSchema.HasAttribute,
            target = sunny
        };
        conditionalRel.Conditions.Add(weatherCondition);
        
        // Exception: Tripper doesn't play outside (leg injury)
        var tripperException = new MetaRelationship
        {
            HyperedgeType = HyperedgeType.ExceptionRelation,
            source = tripper,
            relType = MetaHypergraphSchema.Exception,
            target = playOutside,
            Priority = 10.0f, // Higher priority than general rule
            IsException = true,
            Confidence = 1.0f,
            Source = "direct-observation"
        };
        
        // Add to UKS
        dog.RelationshipsWriteable.Add(conditionalRel);
        tripper.RelationshipsWriteable.Add(tripperException);
    }
}
```

### Query Resolution with Inheritance and Exceptions

```csharp
public class MetaHypergraphQueryEngine
{
    private UKS.UKS uks;
    
    public List<Thing> QueryActions(Thing subject, Dictionary<Thing, Thing> context)
    {
        var actions = new List<Thing>();
        var exceptions = new List<MetaRelationship>();
        var conditions = new List<MetaRelationship>();
        
        // 1. Collect inherited relationships from ancestors
        foreach (Thing ancestor in subject.Ancestors)
        {
            foreach (var rel in ancestor.Relationships.OfType<MetaRelationship>())
            {
                if (rel.HyperedgeType == HyperedgeType.ConditionalRelation)
                    conditions.Add(rel);
                else if (rel.HyperedgeType == HyperedgeType.ExceptionRelation)
                    exceptions.Add(rel);
            }
        }
        
        // 2. Collect direct relationships
        foreach (var rel in subject.Relationships.OfType<MetaRelationship>())
        {
            if (rel.HyperedgeType == HyperedgeType.ExceptionRelation)
                exceptions.Add(rel);
        }
        
        // 3. Apply exceptions first (highest priority)
        exceptions = exceptions.OrderByDescending(e => e.Priority).ToList();
        foreach (var exception in exceptions)
        {
            if (exception.source == subject) // Direct exception
            {
                // This action is explicitly blocked
                continue; // Skip adding to actions
            }
        }
        
        // 4. Evaluate conditional relationships
        foreach (var condition in conditions)
        {
            if (EvaluateConditions(condition.Conditions, context))
            {
                // Check if this is overridden by exception
                bool isOverridden = exceptions.Any(e => 
                    e.target == condition.target && 
                    e.Priority > condition.Priority);
                    
                if (!isOverridden)
                {
                    actions.Add(condition.target);
                }
            }
        }
        
        return actions;
    }
    
    private bool EvaluateConditions(List<Relationship> conditions, Dictionary<Thing, Thing> context)
    {
        foreach (var condition in conditions)
        {
            if (!context.ContainsKey(condition.source) || 
                context[condition.source] != condition.target)
            {
                return false; // All conditions must be met
            }
        }
        return true;
    }
}
```

### Usage Example

```csharp
public void TestQueryEngine()
{
    var queryEngine = new MetaHypergraphQueryEngine();
    
    // Context: sunny weather
    var context = new Dictionary<Thing, Thing>
    {
        [uks.GetOrAddThing("weather")] = uks.GetOrAddThing("sunny")
    };
    
    // Query Fido's actions
    Thing fido = uks.Labeled("fido");
    var fidoActions = queryEngine.QueryActions(fido, context);
    // Result: ["play-outside"] (inherits from dog + weather is sunny)
    
    // Query Tripper's actions  
    Thing tripper = uks.Labeled("tripper");
    var tripperActions = queryEngine.QueryActions(tripper, context);
    // Result: [] (exception overrides inherited behavior)
}
```

## Integration with Current UKS

### 1. Backward Compatibility
- Existing `Thing` and `Relationship` classes remain unchanged
- `MetaRelationship` extends `Relationship` with new capabilities
- Current modules continue to work without modification

### 2. Gradual Migration Path
```csharp
public static class UKSExtensions
{
    // Helper to upgrade existing relationships
    public static MetaRelationship AsMetaRelationship(this Relationship rel)
    {
        return new MetaRelationship
        {
            source = rel.source,
            target = rel.target,
            relType = rel.relType,
            Weight = rel.Weight,
            HyperedgeType = HyperedgeType.SimpleRelation
        };
    }
    
    // Query that handles both types
    public static List<Relationship> GetAllRelationships(this Thing thing)
    {
        var results = new List<Relationship>();
        results.AddRange(thing.Relationships);
        
        // Add inherited meta-relationships
        var metaEngine = new MetaHypergraphQueryEngine();
        // ... apply inheritance and exception resolution
        
        return results;
    }
}
```

### 3. Hybrid Learning Integration
```csharp
public class HybridLearningModule : ModuleBase
{
    // HGNN embeddings for similarity
    private Dictionary<Thing, float[]> embeddings = new();
    
    // Neuro-symbolic rule learning
    private MetaHypergraphQueryEngine ruleEngine = new();
    
    public override void Fire()
    {
        // 1. Update embeddings based on recent relationships
        UpdateEmbeddings();
        
        // 2. Discover new patterns via similarity
        DiscoverSimilarityPatterns();
        
        // 3. Generalize into symbolic rules
        GenerateSymbolicRules();
        
        // 4. Apply rules for inference
        ApplyMetaRules();
    }
    
    private void GenerateSymbolicRules()
    {
        // Example: if multiple dogs exhibit similar behavior patterns,
        // create a conditional meta-relationship at the dog concept level
        
        foreach (Thing concept in theUKS.UKSList.Where(t => t.HasAncestor("concept-node")))
        {
            var instances = concept.Children;
            var commonPatterns = FindCommonBehaviorPatterns(instances);
            
            foreach (var pattern in commonPatterns)
            {
                if (pattern.Confidence > 0.7f)
                {
                    CreateConditionalRule(concept, pattern);
                }
            }
        }
    }
}
```

## Benefits of This Approach

1. **Unified Substrate**: Single formalism handles all five cognitive primitives
2. **Explainable AI**: Graph structure makes reasoning transparent  
3. **Sample Efficiency**: Inheritance reduces redundant storage
4. **Exception Handling**: Natural conflict resolution via priority
5. **Temporal Reasoning**: Built-in support for time-bounded facts
6. **Hybrid Learning**: Combines neural similarity with symbolic rules

## Next Steps

1. Implement `MetaRelationship` class in UKS project
2. Create `MetaHypergraphQueryEngine` module
3. Add hybrid learning module with HGNN integration
4. Extend existing modules to use meta-relationships
5. Create visualization tools for meta-hypergraph structures