namespace DNTU.SkillBridge.Application.Abstractions;

/// <summary>
/// Thrown when committing pending changes to the database fails, for example because of a
/// unique constraint race. The infrastructure unit of work translates the EF Core
/// <c>DbUpdateException</c> into this Application-layer exception, keeping the inner exception.
/// </summary>
public class PersistenceException(string message, Exception innerException) : Exception(message, innerException);
