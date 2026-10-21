using System;

namespace Faemiyah.Repository.IntegrationTests.Fixtures;

/// <summary>
/// Thrown deliberately from provisioning, so a test can tell an induced failure from a real one.
/// </summary>
public class TestProvisioningException : Exception
{
    public TestProvisioningException()
        : base("Provisioning was made to fail by a test.")
    {
    }
}
