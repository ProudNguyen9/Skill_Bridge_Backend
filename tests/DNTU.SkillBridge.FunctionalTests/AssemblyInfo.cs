// Functional tests boot multiple API hosts against the same SQL database. Keep
// those hosts sequential so their startup seeders cannot race each other.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
