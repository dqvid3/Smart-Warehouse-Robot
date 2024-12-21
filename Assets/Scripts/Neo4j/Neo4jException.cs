using System;

public class Neo4jException : Exception
{
    public Neo4jException(string message) : base(message) { }
}
