//
// PROPRIETARY AND CONFIDENTIAL
// Brain Simulator 3 v.1.0
// © 2022 FutureAI, Inc., all rights reserved
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using UKS;

namespace BrainSimulator.Modules
{
    /// <summary>
    /// Module that implements meta-hypergraph query resolution with inheritance, exceptions, and conditional logic.
    /// Provides advanced reasoning capabilities over the UKS knowledge graph.
    /// </summary>
    public class ModuleMetaHypergraphQueryEngine : ModuleBase
    {
        // Public properties for configuration (automatically saved/restored)
        public bool EnableInheritance { get; set; } = true;
        public bool EnableExceptions { get; set; } = true;
        public bool EnableConditionals { get; set; } = true;
        public float DefaultConfidenceThreshold { get; set; } = 0.5f;
        
        [XmlIgnore]
        private Dictionary<Thing, Thing> currentContext = new();

        public ModuleMetaHypergraphQueryEngine()
        {
        }

        /// <summary>
        /// Execute per-cycle processing
        /// </summary>
        public override void Fire()
        {
            Init();
            
            // Process any pending query requests
            ProcessQueryRequests();
            
            UpdateDialog();
        }

        /// <summary>
        /// Initialize the module
        /// </summary>
        public override void Initialize()
        {
            // Initialize the meta-hypergraph schema
            if (theUKS != null)
            {
                MetaHypergraphSchema.Initialize(theUKS);
            }
        }

        /// <summary>
        /// Process any query requests in the UKS
        /// </summary>
        private void ProcessQueryRequests()
        {
            if (theUKS == null) return;
            
            // Look for query request Things
            var queryRequests = theUKS.UKSList.Where(t => 
                t.HasAncestorLabeled("query-request") && 
                t.Relationships.Any(r => r.relType?.Label == "status" && r.target?.Label == "pending")
            ).ToList();
            
            foreach (var queryRequest in queryRequests)
            {
                ProcessQuery(queryRequest);
            }
        }

        /// <summary>
        /// Process a single query request
        /// </summary>
        private void ProcessQuery(Thing queryRequest)
        {
            try
            {
                // Extract query parameters
                var subject = GetQueryParameter(queryRequest, "subject");
                var queryType = GetQueryParameter(queryRequest, "query-type");
                var context = ExtractContext(queryRequest);
                
                if (subject == null) return;
                
                List<Thing> results = new();
                
                switch (queryType?.Label?.ToLower())
                {
                    case "actions":
                        results = QueryActions(subject, context);
                        break;
                    case "attributes":
                        results = QueryAttributes(subject, context);
                        break;
                    case "relationships":
                        results = QueryRelationships(subject, context);
                        break;
                    default:
                        results = QueryActions(subject, context); // Default to actions
                        break;
                }
                
                // Store results
                StoreQueryResults(queryRequest, results);
                
                // Mark query as completed
                MarkQueryCompleted(queryRequest);
            }
            catch (Exception ex)
            {
                // Mark query as failed
                MarkQueryFailed(queryRequest, ex.Message);
            }
        }

        /// <summary>
        /// Query available actions for a subject in the given context
        /// </summary>
        public List<Thing> QueryActions(Thing subject, Dictionary<Thing, Thing> context)
        {
            var actions = new List<Thing>();
            var exceptions = new List<MetaRelationship>();
            var conditions = new List<MetaRelationship>();
            
            if (!EnableInheritance && !EnableConditionals) 
            {
                // Simple query - just direct relationships
                return subject.Relationships
                    .Where(r => r.relType?.HasAncestorLabeled("action-type") == true)
                    .Select(r => r.target)
                    .Where(t => t != null)
                    .ToList();
            }
            
            // 1. Collect inherited relationships from ancestors
            if (EnableInheritance)
            {
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
            }
            
            // 2. Collect direct relationships
            foreach (var rel in subject.Relationships.OfType<MetaRelationship>())
            {
                if (rel.HyperedgeType == HyperedgeType.ExceptionRelation)
                    exceptions.Add(rel);
                else if (rel.HyperedgeType == HyperedgeType.ConditionalRelation)
                    conditions.Add(rel);
            }
            
            // 3. Apply exceptions first (highest priority)
            if (EnableExceptions)
            {
                exceptions = exceptions.OrderByDescending(e => e.Priority).ToList();
                var blockedActions = new HashSet<Thing>();
                
                foreach (var exception in exceptions)
                {
                    if (exception.source == subject || subject.HasAncestor(exception.source))
                    {
                        // This action is explicitly blocked
                        foreach (var target in exception.GetEffectiveTargets())
                        {
                            blockedActions.Add(target);
                        }
                    }
                }
                
                // 4. Evaluate conditional relationships
                if (EnableConditionals)
                {
                    foreach (var condition in conditions)
                    {
                        if (condition.EvaluateConditions(context))
                        {
                            // Check if this is overridden by exception
                            foreach (var target in condition.GetEffectiveTargets())
                            {
                                if (!blockedActions.Contains(target) && 
                                    condition.Confidence >= DefaultConfidenceThreshold)
                                {
                                    actions.Add(target);
                                }
                            }
                        }
                    }
                }
            }
            else if (EnableConditionals)
            {
                // Just evaluate conditionals without exception handling
                foreach (var condition in conditions)
                {
                    if (condition.EvaluateConditions(context) && 
                        condition.Confidence >= DefaultConfidenceThreshold)
                    {
                        actions.AddRange(condition.GetEffectiveTargets());
                    }
                }
            }
            
            return actions.Distinct().ToList();
        }

        /// <summary>
        /// Query attributes for a subject considering inheritance and exceptions
        /// </summary>
        public List<Thing> QueryAttributes(Thing subject, Dictionary<Thing, Thing> context)
        {
            var attributes = new List<Thing>();
            
            // Start with direct attributes
            attributes.AddRange(subject.GetAttributes());
            
            if (EnableInheritance)
            {
                // Add inherited attributes
                foreach (Thing ancestor in subject.Ancestors)
                {
                    attributes.AddRange(ancestor.GetAttributes());
                }
            }
            
            // Apply exceptions if enabled
            if (EnableExceptions)
            {
                var exceptions = subject.Relationships.OfType<MetaRelationship>()
                    .Where(r => r.HyperedgeType == HyperedgeType.ExceptionRelation)
                    .ToList();
                
                foreach (var exception in exceptions.OrderByDescending(e => e.Priority))
                {
                    // Remove overridden attributes
                    foreach (var target in exception.GetEffectiveTargets())
                    {
                        attributes.Remove(target);
                    }
                }
            }
            
            return attributes.Distinct().ToList();
        }

        /// <summary>
        /// Query relationships for a subject
        /// </summary>
        public List<Thing> QueryRelationships(Thing subject, Dictionary<Thing, Thing> context)
        {
            var relationships = new List<Thing>();
            
            // Direct relationships
            relationships.AddRange(subject.Relationships.Select(r => r.target).Where(t => t != null));
            
            if (EnableInheritance)
            {
                // Inherited relationships
                foreach (Thing ancestor in subject.Ancestors)
                {
                    relationships.AddRange(ancestor.Relationships.Select(r => r.target).Where(t => t != null));
                }
            }
            
            return relationships.Distinct().ToList();
        }

        /// <summary>
        /// Extract query parameter by relationship type
        /// </summary>
        private Thing GetQueryParameter(Thing queryRequest, string parameterType)
        {
            return queryRequest.Relationships
                .FirstOrDefault(r => r.relType?.Label == parameterType)?.target;
        }

        /// <summary>
        /// Extract context from query request
        /// </summary>
        private Dictionary<Thing, Thing> ExtractContext(Thing queryRequest)
        {
            var context = new Dictionary<Thing, Thing>();
            
            var contextRels = queryRequest.Relationships
                .Where(r => r.relType?.Label == "context-item")
                .ToList();
            
            foreach (var contextRel in contextRels)
            {
                if (contextRel.target != null)
                {
                    var keyValueRels = contextRel.target.Relationships
                        .Where(r => r.relType?.Label == "has-value")
                        .ToList();
                    
                    foreach (var kvRel in keyValueRels)
                    {
                        if (kvRel.source != null && kvRel.target != null)
                        {
                            context[kvRel.source] = kvRel.target;
                        }
                    }
                }
            }
            
            return context;
        }

        /// <summary>
        /// Store query results back to UKS
        /// </summary>
        private void StoreQueryResults(Thing queryRequest, List<Thing> results)
        {
            if (theUKS == null) return;
            
            // Create result container
            var resultContainer = theUKS.GetOrAddThing("query-result*", "QueryResult");
            queryRequest.AddRelationship(resultContainer, theUKS.Labeled("has-result"));
            
            // Add each result
            foreach (var result in results)
            {
                resultContainer.AddRelationship(result, theUKS.Labeled("contains"));
            }
        }

        /// <summary>
        /// Mark query as completed
        /// </summary>
        private void MarkQueryCompleted(Thing queryRequest)
        {
            if (theUKS == null) return;
            
            // Remove pending status
            var pendingRel = queryRequest.Relationships
                .FirstOrDefault(r => r.relType?.Label == "status" && r.target?.Label == "pending");
            if (pendingRel != null)
            {
                queryRequest.RemoveRelationship(pendingRel);
            }
            
            // Add completed status
            queryRequest.AddRelationship(theUKS.Labeled("completed"), theUKS.Labeled("status"));
        }

        /// <summary>
        /// Mark query as failed with error message
        /// </summary>
        private void MarkQueryFailed(Thing queryRequest, string errorMessage)
        {
            if (theUKS == null) return;
            
            // Remove pending status
            var pendingRel = queryRequest.Relationships
                .FirstOrDefault(r => r.relType?.Label == "status" && r.target?.Label == "pending");
            if (pendingRel != null)
            {
                queryRequest.RemoveRelationship(pendingRel);
            }
            
            // Add failed status
            queryRequest.AddRelationship(theUKS.Labeled("failed"), theUKS.Labeled("status"));
            
            // Add error message
            var errorThing = theUKS.GetOrAddThing(errorMessage, "ErrorMessage");
            queryRequest.AddRelationship(errorThing, theUKS.Labeled("error"));
        }

        public override void SetUpBeforeSave()
        {
            // Clear any temporary state before saving
            currentContext.Clear();
        }

        public override void SetUpAfterLoad()
        {
            // Reinitialize after loading
            currentContext = new Dictionary<Thing, Thing>();
        }
    }
}