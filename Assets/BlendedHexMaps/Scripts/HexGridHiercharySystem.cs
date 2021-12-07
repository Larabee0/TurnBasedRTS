using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Profiling;
using System;
using System.Diagnostics;

namespace DOTSHexagonsV2
{
    [ExecuteAlways]
    public class HexGridParentSystem : JobComponentSystem
    {
        EntityQuery m_NewParentsQuery;
        EntityQuery m_RemovedParentsQuery;
        EntityQuery m_ExistingParentsQuery;

        static readonly ProfilerMarker k_ProfileRemoveParents = new ProfilerMarker("HexGridParentSystem.RemoveParents");
        static readonly ProfilerMarker k_ProfileChangeParents = new ProfilerMarker("HexGridParentSystem.ChangeParents");
        static readonly ProfilerMarker k_ProfileNewParents = new ProfilerMarker("HexGridParentSystem.NewParents");

        int FindChildIndex(DynamicBuffer<HexGridChild> children, Entity entity)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].Value == entity)
                    return i;
            }

            throw new InvalidOperationException("Child entity not in parent (HexGridParentSystem)");
        }

        void RemoveChildFromParent(Entity childEntity, Entity parentEntity)
        {
            if (!EntityManager.HasComponent<HexGridChild>(parentEntity))
                return;

            var children = EntityManager.GetBuffer<HexGridChild>(parentEntity);
            var childIndex = FindChildIndex(children, childEntity);
            children.RemoveAt(childIndex);
            if (children.Length == 0)
            {
                EntityManager.RemoveComponent(parentEntity, typeof(HexGridChild));
            }
        }

        [BurstCompile]
        struct GatherChangedParents : IJobEntityBatch
        {
            public NativeMultiHashMap<Entity, Entity>.ParallelWriter ParentChildrenToAdd;
            public NativeMultiHashMap<Entity, Entity>.ParallelWriter ParentChildrenToRemove;
            public NativeHashMap<Entity, int>.ParallelWriter UniqueParents;
            public ComponentTypeHandle<HexGridPreviousParent> PreviousParentTypeHandle;

            [ReadOnly] public ComponentTypeHandle<HexGridParent> ParentTypeHandle;
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public uint LastSystemVersion;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                if (batchInChunk.DidChange(ParentTypeHandle, LastSystemVersion))
                {
                    var chunkPreviousParents = batchInChunk.GetNativeArray(PreviousParentTypeHandle);
                    var chunkParents = batchInChunk.GetNativeArray(ParentTypeHandle);
                    var chunkEntities = batchInChunk.GetNativeArray(EntityTypeHandle);

                    for (int j = 0; j < batchInChunk.Count; j++)
                    {
                        if (chunkParents[j].Value != chunkPreviousParents[j].Value)
                        {
                            var childEntity = chunkEntities[j];
                            var parentEntity = chunkParents[j].Value;
                            var previousParentEntity = chunkPreviousParents[j].Value;

                            ParentChildrenToAdd.Add(parentEntity, childEntity);
                            UniqueParents.TryAdd(parentEntity, 0);

                            if (previousParentEntity != Entity.Null)
                            {
                                ParentChildrenToRemove.Add(previousParentEntity, childEntity);
                                UniqueParents.TryAdd(previousParentEntity, 0);
                            }

                            chunkPreviousParents[j] = new HexGridPreviousParent
                            {
                                Value = parentEntity
                            };
                        }
                    }
                }
            }
        }

        [BurstCompile]
        struct FindMissingChild : IJob
        {
            [ReadOnly] public NativeHashMap<Entity, int> UniqueParents;
            [ReadOnly] public BufferFromEntity<HexGridChild> ChildFromEntity;
            public NativeList<Entity> ParentsMissingChild;

            public void Execute()
            {
                var parents = UniqueParents.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < parents.Length; i++)
                {
                    var parent = parents[i];
                    if (!ChildFromEntity.HasComponent(parent))
                    {
                        ParentsMissingChild.Add(parent);
                    }
                }
            }
        }

        [BurstCompile]
        struct FixupChangedChildren : IJob
        {
            [ReadOnly] public NativeMultiHashMap<Entity, Entity> ParentChildrenToAdd;
            [ReadOnly] public NativeMultiHashMap<Entity, Entity> ParentChildrenToRemove;
            [ReadOnly] public NativeHashMap<Entity, int> UniqueParents;

            public BufferFromEntity<HexGridChild> ChildFromEntity;

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
            private static void ThrowChildEntityNotInParent()
            {
                throw new InvalidOperationException("Child entity not in parent (HexGridParentSystem)");
            }

            int FindChildIndex(DynamicBuffer<HexGridChild> children, Entity entity)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i].Value == entity)
                        return i;
                }

                ThrowChildEntityNotInParent();
                return -1;
            }

            void RemoveChildrenFromParent(Entity parent, DynamicBuffer<HexGridChild> children)
            {
                if (ParentChildrenToRemove.TryGetFirstValue(parent, out var child, out var it))
                {
                    do
                    {
                        var childIndex = FindChildIndex(children, child);
                        children.RemoveAt(childIndex);
                    }
                    while (ParentChildrenToRemove.TryGetNextValue(out child, ref it));
                }
            }

            void AddChildrenToParent(Entity parent, DynamicBuffer<HexGridChild> children)
            {
                if (ParentChildrenToAdd.TryGetFirstValue(parent, out var child, out var it))
                {
                    do
                    {
                        children.Add(new HexGridChild { Value = child });
                    }
                    while (ParentChildrenToAdd.TryGetNextValue(out child, ref it));
                }
            }

            public void Execute()
            {
                var parents = UniqueParents.GetKeyArray(Allocator.Temp);
                for (int i = 0; i < parents.Length; i++)
                {
                    var parent = parents[i];
                    var children = ChildFromEntity[parent];

                    RemoveChildrenFromParent(parent, children);
                    AddChildrenToParent(parent, children);
                }
            }
        }

        protected override void OnCreate()
        {
            m_NewParentsQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<HexGridParent>(),
                },
                None = new ComponentType[]
                {
                    typeof(HexGridPreviousParent)
                }
            });
            m_RemovedParentsQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(HexGridPreviousParent)
                },
                None = new ComponentType[]
                {
                    typeof(HexGridParent)
                }
            });
            m_ExistingParentsQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<HexGridParent>(),
                    typeof(HexGridPreviousParent)
                }
            });
            m_ExistingParentsQuery.SetChangedVersionFilter(typeof(HexGridParent));
        }

        void UpdateNewParents()
        {
            if (m_NewParentsQuery.IsEmptyIgnoreFilter)
                return;

            EntityManager.AddComponent(m_NewParentsQuery, typeof(HexGridPreviousParent));
        }

        void UpdateRemoveParents()
        {
            if (m_RemovedParentsQuery.IsEmptyIgnoreFilter)
                return;

            var childEntities = m_RemovedParentsQuery.ToEntityArray(Allocator.TempJob);
            var previousParents = m_RemovedParentsQuery.ToComponentDataArray<HexGridPreviousParent>(Allocator.TempJob);

            for (int i = 0; i < childEntities.Length; i++)
            {
                var childEntity = childEntities[i];
                var previousParentEntity = previousParents[i].Value;

                RemoveChildFromParent(childEntity, previousParentEntity);
            }

            EntityManager.RemoveComponent(m_RemovedParentsQuery, typeof(HexGridPreviousParent));
            childEntities.Dispose();
            previousParents.Dispose();
        }

        void UpdateChangeParents()
        {
            if (m_ExistingParentsQuery.IsEmptyIgnoreFilter)
                return;

            var count = m_ExistingParentsQuery.CalculateEntityCount() * 2; // Potentially 2x changed: current and previous
            if (count == 0)
                return;

            // 1. Get (Parent,Child) to remove
            // 2. Get (Parent,Child) to add
            // 3. Get unique Parent change list
            // 4. Set PreviousParent to new Parent
            var parentChildrenToAdd = new NativeMultiHashMap<Entity, Entity>(count, Allocator.TempJob);
            var parentChildrenToRemove = new NativeMultiHashMap<Entity, Entity>(count, Allocator.TempJob);
            var uniqueParents = new NativeHashMap<Entity, int>(count, Allocator.TempJob);
            var gatherChangedParentsJob = new GatherChangedParents
            {
                ParentChildrenToAdd = parentChildrenToAdd.AsParallelWriter(),
                ParentChildrenToRemove = parentChildrenToRemove.AsParallelWriter(),
                UniqueParents = uniqueParents.AsParallelWriter(),
                PreviousParentTypeHandle = GetComponentTypeHandle<HexGridPreviousParent>(false),
                ParentTypeHandle = GetComponentTypeHandle<HexGridParent>(true),
                EntityTypeHandle = GetEntityTypeHandle(),
                LastSystemVersion = LastSystemVersion
            };
            var gatherChangedParentsJobHandle = gatherChangedParentsJob.ScheduleParallel(m_ExistingParentsQuery, 1, default(JobHandle));
            gatherChangedParentsJobHandle.Complete();

            // 5. (Structural change) Add any missing Child to Parents
            var parentsMissingChild = new NativeList<Entity>(Allocator.TempJob);
            var findMissingChildJob = new FindMissingChild
            {
                UniqueParents = uniqueParents,
                ChildFromEntity = GetBufferFromEntity<HexGridChild>(),
                ParentsMissingChild = parentsMissingChild
            };
            var findMissingChildJobHandle = findMissingChildJob.Schedule();
            findMissingChildJobHandle.Complete();

            EntityManager.AddComponent(parentsMissingChild.AsArray(), typeof(HexGridChild));

            // 6. Get Child[] for each unique Parent
            // 7. Update Child[] for each unique Parent
            var fixupChangedChildrenJob = new FixupChangedChildren
            {
                ParentChildrenToAdd = parentChildrenToAdd,
                ParentChildrenToRemove = parentChildrenToRemove,
                UniqueParents = uniqueParents,
                ChildFromEntity = GetBufferFromEntity<HexGridChild>()
            };

            var fixupChangedChildrenJobHandle = fixupChangedChildrenJob.Schedule();
            fixupChangedChildrenJobHandle.Complete();

            parentChildrenToAdd.Dispose();
            parentChildrenToRemove.Dispose();
            uniqueParents.Dispose();
            parentsMissingChild.Dispose();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            inputDeps.Complete(); // #todo

            k_ProfileRemoveParents.Begin();
            UpdateRemoveParents();
            k_ProfileRemoveParents.End();

            k_ProfileNewParents.Begin();
            UpdateNewParents();
            k_ProfileNewParents.End();

            k_ProfileChangeParents.Begin();
            UpdateChangeParents();
            k_ProfileChangeParents.End();

            return new JobHandle();
        }
    }
}
