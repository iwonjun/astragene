using Xunit;
// Performance measurements must not compete with stress tests in the same process.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
