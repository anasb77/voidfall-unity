using NUnit.Framework;
using VoidFall.Core;

namespace VoidFall.Tests.Editor
{
    public sealed class DirectorCapacityGridTests
    {
        private const int Capacity = 750;

        [Test]
        public void Same_cell_query_returns_all_750_identities_in_insertion_order()
        {
            var grid = FilledGrid();
            var output = new int[Capacity];

            var count = grid.QueryNeighborhood(1, 1, 0, output);

            Assert.That(count, Is.EqualTo(Capacity));
            Assert.That(output, Is.Unique);
            for (var index = 0; index < Capacity; index++)
                Assert.That(output[index], Is.EqualTo(index), "Insertion order at " + index);
            Assert.That(output[Capacity - 1], Is.EqualTo(749));
        }

        [Test]
        public void Same_cell_query_with_undersized_output_is_bounded()
        {
            var grid = FilledGrid();
            var output = new int[17];

            var count = grid.QueryNeighborhood(1, 1, 0, output);

            Assert.That(count, Is.EqualTo(output.Length));
            for (var index = 0; index < output.Length; index++)
                Assert.That(output[index], Is.EqualTo(index));
            var completeOutput = new int[Capacity];
            Assert.That(grid.QueryNeighborhood(1, 1, 0, completeOutput), Is.EqualTo(Capacity),
                "A truncated query must not consume or damage the cell chain.");
            Assert.That(completeOutput[Capacity - 1], Is.EqualTo(749));
        }

        private static CollisionGrid FilledGrid()
        {
            var grid = new CollisionGrid(Capacity);
            for (var index = 0; index < Capacity; index++) grid.Insert(index, 1, 1);
            return grid;
        }
    }
}
