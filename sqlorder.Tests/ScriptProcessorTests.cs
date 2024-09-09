namespace sqlorder.Tests;

[TestClass]
public class ScriptProcessorTests
{
    [TestMethod]
    public void ZeroScriptOrderTest()
    {
        var orderedScripts = ScriptProcessor.OrderScripts(new List<Script>());

        Assert.AreEqual(0, orderedScripts.Count());
    }

    [TestMethod]
    public void MigrationScriptOrderTest()
    {
        var s1 = new Script("01.sql", "1", "1");
        var s2 = new Script("02.sql", "2", "2");
        var s3 = new Script("random.sql", "random", "random");
        var scripts = new List<Script> { s3, s2, s1 };

        var orderedScripts = ScriptProcessor.OrderScripts(scripts);

        Assert.AreEqual(orderedScripts.First(), s1);
    }

    [TestMethod]
    public void ProcedureScriptOrderTest()
    {
        var s1 = new Script("proc1.sql", "blah blah blah", "1");
        var s2 = new Script("proc2.sql", "blah blah blah proc1 blah blah", "2");
        var s3 = new Script("random.sql", "random", "random");
        var scripts = new List<Script> { s1, s2, s3 };

        var orderedScripts = ScriptProcessor.OrderScripts(scripts);

        Assert.AreEqual(orderedScripts.Last(), s2);
    }

    [TestMethod]
    public void ComplexProcedureScriptOrderTest()
    {
        var s1 = new Script("proc1.sql", "blah blah blah", "1");
        var s2 = new Script("proc2.sql", "blah blah blah proc1 blah blah", "2");
        var s3 = new Script("random.sql", "random", "random");
        var s4 = new Script("proc3.sql", "blah blah blah proc2 blah blah proc1", "3");
        var scripts = new List<Script> { s1, s2, s3, s4 };

        var orderedScripts = ScriptProcessor.OrderScripts(scripts);

        Assert.AreEqual(orderedScripts.First(), s1);
    }

    [TestMethod]
    public void CyclicDependencyTest()
    {
        var s1 = new Script("proc1.sql", "blah blah blah proc2", "1");
        var s2 = new Script("proc2.sql", "blah blah blah proc1", "2");
        var scripts = new List<Script> { s1, s2 };

        Assert.ThrowsException<Exception>(() => ScriptProcessor.OrderScripts(scripts));
    }

    [TestMethod]
    public void HybridScriptOrderTest()
    {
        var s0 = new Script("01.sql", "1", "1");
        var s1 = new Script("proc1.sql", "blah blah blah", "1");
        var s2 = new Script("proc2.sql", "blah blah blah proc1 blah blah", "2");
        var s3 = new Script("02.sql", "random", "random");
        var scripts = new List<Script> { s0, s1, s2, s3 };

        var orderedScripts = ScriptProcessor.OrderScripts(scripts).ToList();

        Assert.AreEqual(orderedScripts.First(), s0);
        Assert.AreEqual(orderedScripts.ElementAt(1), s3);
        Assert.AreEqual(orderedScripts.Last(), s2);
    }
}