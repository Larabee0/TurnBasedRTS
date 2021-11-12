using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Rendering;
using Unity.Physics;

namespace DOTSHexagons 
{
    /// <summary>
    /// This system handles the user selecting a different grid.
    /// Its purpose is to hide the current grid, and show the grid the user has just selected,
    /// it also marks this new grid as "Active" and also handles hiding and showing grid features.
    /// </summary>
    public class HexGridActivationSystem : JobComponentSystem
    {
        // The entity command buffer systems;
        // these allow you to queue up changes to entities to all run at once in a big batch

        // ecbEnd runs at the end of the frame render cycle
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;

        // ecbBegin runs at the start of the frame render cycle
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;


        // query 1 - gets all entities with a "HexGridComponent", "HexGridCreatedComponent" and "MakeActiveGridEntity"
        private readonly EntityQueryDesc MakeActive = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated),typeof(MakeActiveGridEntity) } };

        // query 2 - gets all entities with a "FeatureGridInfo" component, any feature that exists will have one of these.
        private readonly EntityQueryDesc featureData = new EntityQueryDesc { All = new ComponentType[] { typeof(FeatureGridInfo) } };

        // query 3 - this is is specifically to get an entity that handles the map visability and terrain Material know as the HexCellShader.
        //           This entity will contain the "HexCellShaderDataComponent"
        //           and if the features need changing, it will also have a "SetFeatureVisability" component.
        private readonly EntityQueryDesc featureActivation = new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(SetFeatureVisability) } };

        // query 4 - This query returns all grids that are tagged as "HexGridCreated" i.e. they aren't being generated and aren't being created.
        private readonly EntityQueryDesc AllCreatedGrids = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated) } };

        /// <summary>
        /// OnCreate() is called when the System starts running.
        /// We simply get a refrence to the EntityCommandBuffers we want to use from the Entity World here.
        /// </summary>
        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        /// <summary>
        /// OnUpdate() is called once per frame, its basically Update() like in a MonoBehaviour script
        /// This is a JobComponentSystem however, so we get a job dependency handle input and output.
        /// </summary>
        /// <param name="inputDeps"> Input dependancies for this System</param>
        /// <returns> if we schedule a job we return that job handle , otherwise just return inputDeps.</returns>
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // We will return this JobHandle.
            JobHandle outputDeps;

            // Run a query to see if a grid has requested to become the active grid.
            // We check if its empty, if it is NOT empty we will do something about that.
            EntityQuery ToActivateGridsQuery = GetEntityQuery(MakeActive);
            if (!ToActivateGridsQuery.IsEmpty)
            {
                // Query for all grids in the world.
                EntityQuery AllGridsQuery = GetEntityQuery(AllCreatedGrids);

                // create a grid activation job.
                GridActivation gridActivation = new GridActivation
                {
                    // the shader entity is stored at a static variable in HexCellShaderSystem
                    shaderEntity = HexCellShaderSystem.HexCellShaderData,

                    // We can give makeActive an entity and it will tell us if it has the MakeActiveGridEntity comp.
                    makeActive = GetComponentDataFromEntity<MakeActiveGridEntity>(true),

                    // We can use this to determine the currently active grid.
                    currentActive = GetComponentDataFromEntity<ActiveGridEntity>(true),

                    // This gets us a buffor accessor with all buffers containing HexGridChunks
                    // this is how we turn the grids on and off.
                    chunkAccessors = GetBufferFromEntity<HexGridChunkBuffer>(true),

                    // the HexGridChunkBuffer does not hold the entities the mesh and mesh colliders are on,
                    // we need the HexGridChunkComponent for that, so we get it.
                    chunkComps = GetComponentDataFromEntity<HexGridChunkComponent>(true),

                    // This finally allows us to read the HexGridComponent of the grids we queried for,
                    // we can parse this as a component type handle as the grid MUST have it to register in the query.
                    gridCompTypes = GetComponentTypeHandle<HexGridComponent>(true),

                    // we parse in command buffers from the ecbEnd and Begin systems so this job can make changes.
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer(),
                    ecbEnd = ecbEndSystem.CreateCommandBuffer()
                };

                // now the job is created we need to schedule it, parse in the gid query and the input dependancies.
                // this returns new output dependies in chain with the inputDeps. We assign this to outputDeps.
                outputDeps = gridActivation.Schedule(AllGridsQuery, inputDeps);

                // the entity command buffers need to know to when this job is run so we have to give them the handle for it.
                ecbBeginSystem.AddJobHandleForProducer(outputDeps);
                ecbEndSystem.AddJobHandleForProducer(outputDeps);

                // Note: The jobs system is smart enough not to run this job again until the previous instance has finished executing.
            }
            else // If we get here, the grid Activation System probably won't schedule a job this frame, assign inputDeps to our output.
            {
                outputDeps = inputDeps;
            }

            // check to see if the shader wants to update the status of any features on the grid.
            // if it does we will do something. At this point if not, we just return outputDeps as it now has a value.
            EntityQuery featureActivationQuery = GetEntityQuery(featureActivation);
            if (!featureActivationQuery.IsEmpty)
            {
                // Query for all the features spawned in the world.
                EntityQuery features = GetEntityQuery(featureData);

                // Now features need activating, this is occurs after the shader has reinitialised to a new grid.
                // so its likely this job will not be queued at the same time as the above job.
                FeatureActivation featureActivation = new FeatureActivation
                {
                    // to get the active grid entity, we can just access teh staticfield in HexMetrics.
                    // this is set by the shader once its reinitialised.
                    ActiveGrid = HexMetrics.ActiveGridEntity,

                    // we only get type handles for this job, which means less overhead.
                    // first up th FeatureGridInfo component, this tells the job, what grid this feature belongs to.
                    feautreDataTypeHandle = GetComponentTypeHandle<FeatureGridInfo>(true),

                    // this is a buffer type handle lets us access buffers from the batchInChunk variable.
                    // Some features have child and sometimes those children have childrem, but the all features
                    // have a LinkedEntityGroup, so the child hierachy is irrlevent for this system, which is good.
                    childAccess = GetBufferTypeHandle<LinkedEntityGroup>(true),

                    // our command buffers again.
                    ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
                };

                // this job is being ScheduleParallel-ed, this is because features will number in the 1000s to 100 000s
                // We divide up the query into batches of 64 and execute them in parallel across as many CPU cores as the 
                // Jobs Scehduler decides.
                outputDeps = featureActivation.ScheduleParallel(features, 64, outputDeps);

                // the commmand buffers still need that job handle.
                ecbEndSystem.AddJobHandleForProducer(outputDeps);
                ecbBeginSystem.AddJobHandleForProducer(outputDeps);

                // this time we remove the "SetFeatureVisability" from the shader entity, we could do this inside the job, but as is run in parallel,
                // this would add potentially hundreds of ducplicate commands to the ecbEnd system, which its nice to avoid if possible.
                ecbEndSystem.CreateCommandBuffer().RemoveComponent<SetFeatureVisability>(HexCellShaderSystem.HexCellShaderData);
            }
            return outputDeps;
        }

        /// <summary>
        /// this is an Entity Batch Job compiled using Burst.
        /// And entity batch job runs on a batch of entities gathered using an entity query.
        /// It can process them serially with job.Schedule() or in parallel with job.ScheduleParallel()
        /// Because indexing of the grids is important here, we want to run this in serial.
        /// It is a small Job so that doesn't effect performance much.
        /// 
        /// Interesting Note: Marking most variables in the job as ReadOnly helps the scheduler run slightly faster,
        /// espeically if compiled with burst.
        /// </summary>
        [BurstCompile]
        private struct GridActivation : IJobEntityBatch
        {
            [ReadOnly] // This is the entity the shader system uses to make decisions and store some data between frames.
            public Entity shaderEntity;

            [ReadOnly] // Access to all MakeActiveGridEntity components in the world.
            public ComponentDataFromEntity<MakeActiveGridEntity> makeActive;

            [ReadOnly] // Access to all ActiveGridEntity components in the world.
            public ComponentDataFromEntity<ActiveGridEntity> currentActive;

            [ReadOnly] // Access to all HexGridChunkBuffers  in the world. (basically an array of type HexGridChunkBuffer)
            public BufferFromEntity<HexGridChunkBuffer> chunkAccessors;

            [ReadOnly] // Access to all HexGridChunkComponent in the world.
            public ComponentDataFromEntity<HexGridChunkComponent> chunkComps;

            [ReadOnly] // Access to the HexGridComponent on the grids.
            public ComponentTypeHandle<HexGridComponent> gridCompTypes;

            // entity command buffers.
            // I use ecbEnd for "remove/delete" commands and ecbBegin for "add/create" commands.
            public EntityCommandBuffer ecbEnd;
            public EntityCommandBuffer ecbBegin;


            /// <summary>
            /// All jobs have an execute method, its what is called when the job runs.
            /// this is an EntityBatch job, so we have a batch provided as well as the index of this batch.
            /// </summary>
            /// <param name="batchInChunk"> Contains all the entities and their components found by the query for this batch. </param>
            /// <param name="batchIndex"> Index of the batch, in serial this *should* always be 0</param>
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                // We need component type handles to access data from the batchInChunk structure.
                // We then can get NativeArray's of data or buffer accessors from the chunk.
                // here we get a NativeArray of all the "HexGridComponents" basically, a NativeArray grids.
                // as HexGridComponent contains the entity it is attached to in a variable called "gridEntity".
                NativeArray<HexGridComponent> grids = batchInChunk.GetNativeArray(gridCompTypes);

                // run through all the grids.
                for (int i = 0; i < grids.Length; i++)
                {
                    // cache the grid entity.
                    Entity grid = grids[i].gridEntity;

                    // check if this grid has the "MakeActiveGridEntity" which tells us this grid
                    // is now the active one, make it visible and interactable.
                    if (makeActive.HasComponent(grid))
                    {
                        // a dynamic buffer is used to store array's of data in the entity world.
                        // we need the array of chunks the grid we want to make active has.
                        // we need to loop through all the chunks and make them visible.
                        DynamicBuffer<HexGridChunkBuffer> chunks = chunkAccessors[grid];
                        for (int c = 0; c < chunks.Length; c++)
                        {
                            // the buffer does not store the entities for each mesh, this is stored
                            // on the gridChunk itself so we mus now get it, the buffer does store the chunkEntity.
                            HexGridChunkComponent chunk = chunkComps[chunks[c].ChunkEntity];      
                            
                            // now we queue up the remove component command on all the meshes in the chunk
                            // the component that hides them is called DisableRenderin, it is a simple tag component.
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityEstuaries);
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityRiver);
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityRoads);
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityTerrian);
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityWalls);
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityWater);
                            ecbBegin.RemoveComponent<DisableRendering>(chunk.entityWaterShore);

                            // we also need to remove the PhysicsExclude from the main terrain entity,
                            // so the user can interact with the grid.
                            // this component lets us perseve physics data without having to simulate it.
                            ecbBegin.RemoveComponent<PhysicsExclude>(chunk.entityTerrian);
                        }

                        // the grid will become active at the start of the frame this job is executed before.
                        // we need to remove the "MakeActiveGridEntity" tag to prevent this job running again.
                        ecbEnd.RemoveComponent<MakeActiveGridEntity>(grid);

                        // we also need to add the "ActiveGridEntity" tag so other systems can work properly.
                        ecbBegin.AddComponent<ActiveGridEntity>(grid);

                        // Finally we need to reinitialise the Shader for this grid. So lets queue that action up.
                        ecbBegin.AddComponent(shaderEntity, new InitialiseHexCellShader { grid = grid, x = grids[i].cellCountX, z = grids[i].cellCountZ });
                    }

                    // Any grid without the "MakeActiveGridEntity" component will get into this if statement.
                    // we can do this after the above code because nothing has actually changed yet, so the now "ActiveGrid" still has
                    // its "MakeActiveGridEntity" and thus won't get throught to this.
                    // It should really be an else statement but what are you gonna do?
                    if (!makeActive.HasComponent(grid))
                    {
                        // same as above just this time we are adding the components,
                        // if the entities already have them nothing happens.
                        DynamicBuffer<HexGridChunkBuffer> chunks = chunkAccessors[grid];
                        for (int c = 0; c < chunks.Length; c++)
                        {
                            HexGridChunkComponent chunk = chunkComps[chunks[c].ChunkEntity];                            
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityEstuaries);
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityRiver);
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityRoads);
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityTerrian);
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityWalls);
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityWater);
                            ecbBegin.AddComponent<DisableRendering>(chunk.entityWaterShore);
                            ecbBegin.AddComponent<PhysicsExclude>(chunk.entityTerrian);
                        }
                    }

                    // finally we get the old active grid, and remove its "ActiveGridEntity" component.
                    if (currentActive.HasComponent(grid))
                    {
                        ecbEnd.RemoveComponent<ActiveGridEntity>(grid);
                    }
                }
            }
        }

        /// <summary>
        /// This job is another EntityBatch and handles what features are being displayed.
        /// It determines this by looking at each feature, looking at the grid it belongs to,
        /// and applies or removes the DisableRendering component according to whether that grid is the active gird.
        /// </summary>
        [BurstCompile]
        private struct FeatureActivation : IJobEntityBatch
        {
            // this is our active gird. If the feautre's grid matches this, it can have DisableRendering removed from it.
            [ReadOnly]
            public Entity ActiveGrid;

            // this is how we get what grid the feature belongs to.
            [ReadOnly]
            public ComponentTypeHandle<FeatureGridInfo> feautreDataTypeHandle;

            // this is how we access all children of the feature
            [ReadOnly]
            public BufferTypeHandle<LinkedEntityGroup> childAccess;

            // command buffers, this job is using ScheduleParallel, so they are ParallelWriter command buffers.
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            
            // Execute same as before.
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                // array of all the feature's and their respective grids in this batch.
                NativeArray<FeatureGridInfo> featureDataContainers = batchInChunk.GetNativeArray(feautreDataTypeHandle);

                // provides access to all the feature entities in the current batch.
                BufferAccessor<LinkedEntityGroup> entityGroups = batchInChunk.GetBufferAccessor(childAccess);

                // we loop through all the features in the batch once.
                for (int fDCIndex = 0; fDCIndex < featureDataContainers.Length; fDCIndex++)
                {
                    // cache the featureData (grid entity).
                    FeatureGridInfo featureData = featureDataContainers[fDCIndex];

                    // get all entities that make up that feature.
                    DynamicBuffer<LinkedEntityGroup> allChildren = entityGroups[fDCIndex];

                    // simple switch statement, does this feature's grid entity match the active grid entity?
                    switch (featureData.GridEntity == ActiveGrid)
                    {
                        case true:
                            // Case true, make this feature visible by removing
                            // DisableRendering from all the entities that make it up
                            for (int i = 0; i < allChildren.Length; i++)
                            {
                                Entity childEntity = allChildren[i].Value;
                                ecbEnd.RemoveComponent<DisableRendering>(childEntity.Index, childEntity);
                            }
                            break;
                        case false:
                            // Case false, make this feature invisible by adding
                            // DisableRendering to all the entities that make it up
                            for (int i = 0; i < allChildren.Length; i++)
                            {
                                Entity childEntity = allChildren[i].Value;
                                ecbEnd.AddComponent<DisableRendering>(childEntity.Index, childEntity);
                            }
                            break;
                    }
                }
            }
        }
    }
}