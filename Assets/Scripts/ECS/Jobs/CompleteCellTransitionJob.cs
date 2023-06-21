using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// this job runs immidately after <seealso cref="TransitionCellsJob"/> as that job runs in parallel so cannot remove
/// transitioned cells while maintaining thread safety. Instead it adds transitioned cells to a ParallelWriter NativeList
/// (A thread safe list that multiple threads can add items to at once).
/// 
/// This removes cells from the transitioningCells list. The cells to be removed are contained in the transitionedCells list.
/// They refer to indices in the transitioningCells list to make removal as efficient as possible.
/// 
/// transitionedCells list must first be sorted so the smallest index in it is at i = 0.
/// The list is then iterated throughb backwards, so we remove the largest index first from transitioningCells.
/// if we moved from i = 0 to i = transitionedCells.Length then by the time we get to the end of the list, transitioningCells
/// may no longer contain the item at i = transitionedCells.Length, because its been removed, but worse than that the wrong items
/// will get removed. Starting at the back ensures the order is correct and avoid any index out of range execpetions.
/// </summary>
[BurstCompile]
public struct CompleteCellTransitionJob : IJob
{
    public NativeList<int> transitioningCells;
    public NativeList<int> transitionedCells;

    public void Execute()
    {
        transitionedCells.Sort();

        // this may need iterating through backwards
        for (int i = transitionedCells.Length - 1; i >= 0; i--)
        {
            transitioningCells.RemoveAt(transitionedCells[i]);
        }
    }
}