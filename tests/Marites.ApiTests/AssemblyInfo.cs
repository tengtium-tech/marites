// Environment variables are process-wide, and the auth tests build hosts in different modes.
// Running test classes in parallel would let one class's mode leak into another's host.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
