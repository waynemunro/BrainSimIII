//
// PROPRIETARY AND CONFIDENTIAL
// Brain Simulator 3 v.1.0
// © 2022 FutureAI, Inc., all rights reserved
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Security.Cryptography;
using UKS;

namespace BrainSimulator.Modules
{
    /// <summary>
    /// Hybrid learning module that combines HGNN embeddings for similarity detection
    /// with neuro-symbolic rule learning for pattern generalization and reasoning.
    /// </summary>
    public class ModuleHybridLearning : ModuleBase
    {
        // Configuration parameters (automatically saved/restored)
        public float SimilarityThreshold { get; set; } = 0.7f;
        public float ConfidenceThreshold { get; set; } = 0.7f;
        public int EmbeddingDimensions { get; set; } = 128;
        public int MaxPatternsPerCycle { get; set; } = 10;
        public bool EnablePatternMining { get; set; } = true;
        public bool EnableRuleGeneration { get; set; } = true;

        // Runtime state (not saved)
        [XmlIgnore]
        private Dictionary<Thing, float[]> embeddings = new();
        
        [XmlIgnore]
        private ModuleMetaHypergraphQueryEngine queryEngine = new();
        
        [XmlIgnore]
        private RandomNumberGenerator rng = RandomNumberGenerator.Create();

        public ModuleHybridLearning()
        {
        }

        /// <summary>
        /// Execute per-cycle processing
        /// </summary>
        public override void Fire()
        {
            Init();

            if (theUKS == null) return;

            try
            {
                // 1. Update embeddings based on recent relationships
                if (EnablePatternMining)
                {
                    UpdateEmbeddings();
                }

                // 2. Discover new patterns via similarity
                if (EnablePatternMining)
                {
                    DiscoverSimilarityPatterns();
                }

                // 3. Generalize into symbolic rules
                if (EnableRuleGeneration)
                {
                    GenerateSymbolicRules();
                }

                // 4. Apply meta-rules for inference
                ApplyMetaRules();
            }
            catch (ArgumentNullException ex)
            {
                Console.WriteLine($"HybridLearning argument error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"HybridLearning operation error: {ex.Message}");
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine($"HybridLearning null reference: {ex.Message}");
            }
            catch
            {
                // Rethrow unexpected exceptions to avoid masking critical failures
                throw;
            }

            UpdateDialog();
        }

        /// <summary>
        /// Initialize the module
        /// </summary>
        public override void Initialize()
        {
            embeddings.Clear();
            rng = RandomNumberGenerator.Create();
            
            // Initialize the meta-hypergraph schema
            // Initialize the meta-hypergraph schema
            if (theUKS != null)
            {
                MetaHypergraphSchema.Initialize(theUKS);
                queryEngine.GetUKS(); // Initialize the query engine's UKS reference
            }
        }

        /// <summary>
        /// Update HGNN embeddings based on recent relationship changes
        /// </summary>
        private void UpdateEmbeddings()
        {
            if (theUKS == null) return;

            // Simple embedding update - in a real implementation this would use proper HGNN
            var recentlyModifiedThings = theUKS.UKSList
                .Where(t => t.lastFiredTime > DateTime.Now.AddMinutes(-1))
                .Take(MaxPatternsPerCycle)
                .ToList();

            foreach (var thing in recentlyModifiedThings)
            {
                if (!embeddings.ContainsKey(thing))
                {
                    embeddings[thing] = GenerateRandomEmbedding();
                }

                // Update embedding based on relationships
                UpdateThingEmbedding(thing);
            }
        }

        /// <summary>
        /// Generate a random initial embedding
        private float[] GenerateRandomEmbedding()
        {
            var embedding = new float[EmbeddingDimensions];
            var buffer = new byte[4];
            for (int i = 0; i < EmbeddingDimensions; i++)
            {
                rng.GetBytes(buffer);
                uint value = BitConverter.ToUInt32(buffer, 0);
                float sample = value / (float)uint.MaxValue; // 0..1
                embedding[i] = sample * 2f - 1f; // -1 to 1
            }
            return embedding;
        }

        /// <summary>
        /// Update a thing's embedding based on its relationships
        /// </summary>
        private void UpdateThingEmbedding(Thing thing)
        {
            if (!embeddings.TryGetValue(thing, out var embedding)) return;
        
            var learningRate = 0.01f;
        
            // Collect embeddings of related things (avoid multiple dictionary lookups)
            var relatedEmbeddings = new List<float[]>();
            foreach (var rel in thing.Relationships)
            {
                var target = rel.target;
                if (target != null && embeddings.TryGetValue(target, out var targetEmbedding))
                    relatedEmbeddings.Add(targetEmbedding);
            }
        
            if (relatedEmbeddings.Count > 0)
            {
                for (int i = 0; i < EmbeddingDimensions; i++)
                {
                    var avgValue = relatedEmbeddings.Average(e => e[i]);
                    embedding[i] += learningRate * (avgValue - embedding[i]);
                }
            }
        }

        /// <summary>
        /// Discover similarity patterns between things based on embeddings
        /// </summary>
        private void DiscoverSimilarityPatterns()
        {
            if (theUKS == null || embeddings.Count < 2) return;

            var thingsToCompare = embeddings.Keys.Take(MaxPatternsPerCycle * 2).ToList();

            for (int i = 0; i < thingsToCompare.Count - 1; i++)
            {
                for (int j = i + 1; j < thingsToCompare.Count; j++)
                {
                    var thing1 = thingsToCompare[i];
                    var thing2 = thingsToCompare[j];

                    var similarity = ComputeCosineSimilarity(embeddings[thing1], embeddings[thing2]);

                    if (similarity > SimilarityThreshold)
                    {
                        // Create or strengthen similarity relationship
                        CreateSimilarityRelationship(thing1, thing2, similarity);
                    }
                }
            }
        }

        /// <summary>
        /// Compute cosine similarity between two embeddings
        /// </summary>
        private static float ComputeCosineSimilarity(float[] embedding1, float[] embedding2)
        {
            if (embedding1.Length != embedding2.Length) return 0.0f;
        
            float dotProduct = 0.0f;
            float norm1 = 0.0f;
            float norm2 = 0.0f;
        
            for (int i = 0; i < embedding1.Length; i++)
            {
                dotProduct += embedding1[i] * embedding2[i];
                norm1 += embedding1[i] * embedding1[i];
                norm2 += embedding2[i] * embedding2[i];
            }
        
            if (norm1 == 0.0f || norm2 == 0.0f) return 0.0f;
        
            return dotProduct / (float)(Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        /// <summary>
        /// Create a similarity relationship between two things
        /// </summary>
        private void CreateSimilarityRelationship(Thing thing1, Thing thing2, float similarity)
        {
            if (theUKS == null) return;

            var similarityType = theUKS.GetOrAddThing("similarity", "RelationType");
            
            // Check if relationship already exists
            var existingRel = thing1.Relationships
                .OfType<MetaRelationship>()
                .FirstOrDefault(r => r.relType == similarityType && r.target == thing2);

            if (existingRel == null)
            {
                // Create new similarity relationship
                var simRel = new MetaRelationship
                {
                    source = thing1,
                    target = thing2,
                    relType = similarityType,
                    HyperedgeType = HyperedgeType.ProvenanceRelation,
                    Confidence = similarity,
                    Source = "hybrid-learning",
                    AcquiredAt = DateTime.Now
                };

                thing1.RelationshipsWriteable.Add(simRel);
            }
            else
            {
                // Update existing relationship confidence
                existingRel.Confidence = Math.Max(existingRel.Confidence, similarity);
            }
        }

        /// <summary>
        /// Generate symbolic rules from discovered patterns
        /// </summary>
        private void GenerateSymbolicRules()
        {
            if (theUKS == null) return;

            // Find concept nodes that have multiple similar instances
            var conceptNodes = theUKS.UKSList
                .Where(t => t.HasAncestorLabeled("concept-node"))
                .Take(MaxPatternsPerCycle)
                .ToList();

            foreach (var concept in conceptNodes)
            {
                var instances = concept.Children.ToList();
                var commonPatterns = FindCommonBehaviorPatterns(instances);

                foreach (var pattern in commonPatterns)
                {
                    if (pattern.Confidence > ConfidenceThreshold)
                    {
                        CreateConditionalRule(concept, pattern);
                    }
                }
            }
        }

        /// <summary>
        /// Find common behavior patterns among a set of instances
        /// </summary>
        private static List<BehaviorPattern> FindCommonBehaviorPatterns(List<Thing> instances)
        {
            var patterns = new List<BehaviorPattern>();

            if (instances.Count < 2) return patterns;

            // Look for common relationship patterns
            var relationshipCounts = new Dictionary<string, int>();
            var relationshipExamples = new Dictionary<string, Relationship>();

            foreach (var instance in instances)
            {
                foreach (var rel in instance.Relationships)
                {
                    if (rel.relType?.Label != null && rel.target?.Label != null)
                    {
                        var patternKey = $"{rel.relType.Label}->{rel.target.Label}";
                        relationshipCounts[patternKey] = relationshipCounts.GetValueOrDefault(patternKey, 0) + 1;
                        relationshipExamples[patternKey] = rel;
                    }
                }
            }

            // Find patterns that appear in most instances
            var threshold = (int)Math.Ceiling(instances.Count * 0.6); // 60% of instances

            foreach (var kvp in relationshipCounts.Where(kv => kv.Value >= threshold))
            {
                var confidence = (float)kvp.Value / instances.Count;
                var example = relationshipExamples[kvp.Key];
                
                patterns.Add(new BehaviorPattern
                {
                    RelationType = example.relType,
                    Target = example.target,
                    Confidence = confidence,
                    InstanceCount = kvp.Value
                });
            }

            return patterns;
        }

        /// <summary>
        /// Create a conditional rule from a behavior pattern
        /// </summary>
        private void CreateConditionalRule(Thing concept, BehaviorPattern pattern)
        {
            if (theUKS == null) return;

            // Create conditional meta-relationship
            var conditionalRel = new MetaRelationship
            {
                HyperedgeType = HyperedgeType.ConditionalRelation,
                source = concept,
                relType = MetaHypergraphSchema.Conditional,
                target = pattern.Target,
                Confidence = pattern.Confidence,
                Source = "pattern-learning",
                AcquiredAt = DateTime.Now
            };

            // Add conditions (in this simple case, just membership in the concept)
            var membershipCondition = new Relationship
            {
                source = concept,
                relType = MetaHypergraphSchema.ClassMembership,
                target = concept // Self-reference for membership
            };
            conditionalRel.Conditions.Add(membershipCondition);

            concept.RelationshipsWriteable.Add(conditionalRel);
        }

        /// <summary>
        /// Apply discovered meta-rules for inference
        /// </summary>
        private void ApplyMetaRules()
        {
            if (theUKS == null) return;

            // Look for things that might benefit from rule application
            var candidateThings = theUKS.UKSList
                .Where(t => t.Relationships.Count > 0 && t.Children.Count == 0) // Leaf nodes
                .Take(MaxPatternsPerCycle)
                .ToList();

            foreach (var thing in candidateThings)
            {
                // Apply meta-rules through the query engine
                var context = BuildContextForThing(thing);
                var inferredActions = queryEngine.QueryActions(thing, context);

                // Add inferred relationships with lower confidence
                foreach (var action in inferredActions)
                {
                    CreateInferredRelationship(thing, action);
                }
            }
        }

        /// <summary>
        /// Build context for a thing based on its current state
        /// </summary>
        private static Dictionary<Thing, Thing> BuildContextForThing(Thing thing)
        {
            var context = new Dictionary<Thing, Thing>();

            // Add current attributes as context
            foreach (var attr in thing.GetAttributes())
            {
                if (attr != null)
                {
                    context[thing] = attr;
                }
            }

            return context;
        }

        /// <summary>
        /// Create an inferred relationship with provenance tracking
        /// </summary>
        private void CreateInferredRelationship(Thing source, Thing target)
        {
            if (theUKS == null) return;

            // Check if relationship already exists
            var existingRel = source.Relationships
                .FirstOrDefault(r => r.target == target);

            if (existingRel == null)
            {
                var inferredRel = new MetaRelationship
                {
                    source = source,
                    target = target,
                    relType = theUKS.GetOrAddThing("inferred-action", "RelationType"),
                    HyperedgeType = HyperedgeType.ProvenanceRelation,
                    Confidence = 0.5f, // Lower confidence for inferred relationships
                    Source = "meta-rule-inference",
                    AcquiredAt = DateTime.Now
                };

                source.RelationshipsWriteable.Add(inferredRel);
            }
        }
        public override void SetUpBeforeSave()
        {
            // No special actions required before saving
        }

        public override void SetUpAfterLoad()
        {
            // Reinitialize runtime state after loading
            embeddings = new Dictionary<Thing, float[]>();
            queryEngine = new ModuleMetaHypergraphQueryEngine();
            rng = RandomNumberGenerator.Create();
        }
    }
    /// <summary>
    /// Represents a discovered behavior pattern
    /// </summary>
    public class BehaviorPattern
    {
        public Thing RelationType { get; set; }
        public Thing Target { get; set; }
        public float Confidence { get; set; }
        public int InstanceCount { get; set; }
    }
}