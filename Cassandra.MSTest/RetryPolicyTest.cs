using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyTest;
using System.Threading;
using System.Diagnostics;

namespace Cassandra.MSTest
{
    public class RetryPolicyTests : PolicyTestTools
    {

        public class TestRetryPolicy : IRetryPolicy
        {

            public RetryDecision OnReadTimeout(Query query, ConsistencyLevel cl, int requiredResponses, int receivedResponses, bool dataRetrieved, int nbRetry)
            {
                return RetryDecision.Rethrow();
            }

            public RetryDecision OnWriteTimeout(Query query, ConsistencyLevel cl, string writeType, int requiredAcks, int receivedAcks, int nbRetry)
            {
                return RetryDecision.Rethrow();
            }

            public RetryDecision OnUnavailable(Query query, ConsistencyLevel cl, int requiredReplica, int aliveReplica, int nbRetry)
            {
                return RetryDecision.Rethrow();
            }
        }

        [TestMethod]
        [WorksForMe]
        public void defaultRetryPolicy()
        {
            var builder = Cluster.Builder();
            defaultPolicyTest(builder);
        }

        [TestMethod]
        [WorksForMe]
        public void defaultLoggingPolicy()
        {
            var builder = Cluster.Builder().WithRetryPolicy(new LoggingRetryPolicy(DefaultRetryPolicy.Instance));
            defaultPolicyTest(builder);
        }

        /*
         * Test the FallthroughRetryPolicy.
         * Uses the same code that DefaultRetryPolicy uses.
         */
        [TestMethod]
        [WorksForMe]
        public void fallthroughRetryPolicy()
        {
            var builder = Cluster.Builder().WithRetryPolicy(FallthroughRetryPolicy.Instance);
            defaultPolicyTest(builder);
        }

        /*
         * Test the FallthroughRetryPolicy with Logging enabled.
         * Uses the same code that DefaultRetryPolicy uses.
         */
        [TestMethod]
        [WorksForMe]
        public void fallthroughLoggingPolicy()
        {
            var builder = Cluster.Builder().WithRetryPolicy(new LoggingRetryPolicy(FallthroughRetryPolicy.Instance));
            defaultPolicyTest(builder);
        }

        public void defaultPolicyTest(Builder builder)
        {
            CCMBridge.CCMCluster c = CCMBridge.CCMCluster.Create(2, builder);
            createSchema(c.Session);

            // FIXME: Race condition where the nodes are not fully up yet and assertQueried reports slightly different numbers with fallthrough*Policy
            Thread.Sleep(5000);
            try
            {
                init(c, 12);
                query(c, 12);

                assertQueried(CCMBridge.IP_PREFIX + "1", 6);
                assertQueried(CCMBridge.IP_PREFIX + "2", 6);

                resetCoordinators();

                // Test reads
                bool successfulQuery = false;
                bool readTimeoutOnce = false;
                bool unavailableOnce = false;
                bool restartOnce = false;
                for (int i = 0; i < 100; ++i)
                {
                    try
                    {
                        // Force a ReadTimeoutException to be performed once
                        if (!readTimeoutOnce)
                        {
                            c.CassandraCluster.ForceStop(2);
                        }

                        // Force an UnavailableException to be performed once
                        if (readTimeoutOnce && !unavailableOnce)
                        {
                            TestUtils.waitForDownWithWait(CCMBridge.IP_PREFIX + "2", c.Cluster, 5);
                        }

                        // Bring back node to ensure other errors are not thrown on restart
                        if (unavailableOnce && !restartOnce)
                        {
                            c.CassandraCluster.Start(2);
                            restartOnce = true;
                        }

                        query(c, 12);

                        if (restartOnce)
                            successfulQuery = true;
                    }
                    catch (UnavailableException e)
                    {
                        //                        Assert.Equal("Not enough replica available for query at consistency ONE (1 required but only 0 alive)".ToLower(), e.Message.ToLower());
                        unavailableOnce = true;
                    }
                    catch (ReadTimeoutException e)
                    {
                        //                        Assert.Equal("Cassandra timeout during read query at consistency ONE (1 responses were required but only 0 replica responded)".ToLower(), e.Message.ToLower());
                        readTimeoutOnce = true;
                    }
                }

                // Ensure the full cycle was completed
                Assert.True(successfulQuery, "Hit testing race condition. [Never completed successfully.] (Shouldn't be an issue.):\n");
                Assert.True(readTimeoutOnce, "Hit testing race condition. [Never encountered a ReadTimeoutException.] (Shouldn't be an issue.):\n");
                Assert.True(unavailableOnce, "Hit testing race condition. [Never encountered an UnavailableException.] (Shouldn't be an issue.):\n");

                // A weak test to ensure that the nodes were contacted
                assertQueriedAtLeast(CCMBridge.IP_PREFIX + "1", 1);
                assertQueriedAtLeast(CCMBridge.IP_PREFIX + "2", 1);

                resetCoordinators();


                // Test writes
                successfulQuery = false;
                bool writeTimeoutOnce = false;
                unavailableOnce = false;
                restartOnce = false;
                for (int i = 0; i < 100; ++i)
                {
                    try
                    {
                        // Force a WriteTimeoutException to be performed once
                        if (!writeTimeoutOnce)
                        {
                            c.CassandraCluster.ForceStop(2);
                        }

                        // Force an UnavailableException to be performed once
                        if (writeTimeoutOnce && !unavailableOnce)
                        {
                            TestUtils.waitForDownWithWait(CCMBridge.IP_PREFIX + "2", c.Cluster, 5);
                        }

                        // Bring back node to ensure other errors are not thrown on restart
                        if (unavailableOnce && !restartOnce)
                        {
                            c.CassandraCluster.Start(2);
                            restartOnce = true;
                        }

                        init(c, 12);

                        if (restartOnce)
                            successfulQuery = true;
                    }
                    catch (UnavailableException e)
                    {
                        //                        Assert.Equal("Not enough replica available for query at consistency ONE (1 required but only 0 alive)".ToLower(), e.Message.ToLower());
                        unavailableOnce = true;
                    }
                    catch (WriteTimeoutException e)
                    {
                        //                        Assert.Equal("Cassandra timeout during write query at consistency ONE (1 replica were required but only 0 acknowledged the write)".ToLower(), e.Message.ToLower());
                        writeTimeoutOnce = true;
                    }
                }
                // Ensure the full cycle was completed
                Assert.True(successfulQuery, "Hit testing race condition. [Never completed successfully.] (Shouldn't be an issue.):\n");
                Assert.True(writeTimeoutOnce, "Hit testing race condition. [Never encountered a ReadTimeoutException.] (Shouldn't be an issue.):\n");
                Assert.True(unavailableOnce, "Hit testing race condition. [Never encountered an UnavailableException.] (Shouldn't be an issue.):\n");

                // TODO: Missing test to see if nodes were written to

                // Test batch writes
                successfulQuery = false;
                writeTimeoutOnce = false;
                unavailableOnce = false;
                restartOnce = false;
                for (int i = 0; i < 100; ++i)
                {
                    try
                    {
                        // Force a WriteTimeoutException to be performed once
                        if (!writeTimeoutOnce)
                        {
                            c.CassandraCluster.ForceStop(2);
                        }

                        // Force an UnavailableException to be performed once
                        if (writeTimeoutOnce && !unavailableOnce)
                        {
                            TestUtils.waitForDownWithWait(CCMBridge.IP_PREFIX + "2", c.Cluster, 5);
                        }

                        // Bring back node to ensure other errors are not thrown on restart
                        if (unavailableOnce && !restartOnce)
                        {
                            c.CassandraCluster.Start(2);
                            restartOnce = true;
                        }

                        init(c, 12, true);

                        if (restartOnce)
                            successfulQuery = true;
                    }
                    catch (UnavailableException e)
                    {
                        //                        Assert.Equal("Not enough replica available for query at consistency ONE (1 required but only 0 alive)", e.Message);
                        unavailableOnce = true;
                    }
                    catch (WriteTimeoutException e)
                    {
                        //                        Assert.Equal("Cassandra timeout during write query at consistency ONE (1 replica were required but only 0 acknowledged the write)", e.Message);
                        writeTimeoutOnce = true;
                    }
                }
                // Ensure the full cycle was completed
                Assert.True(successfulQuery, "Hit testing race condition. [Never completed successfully.] (Shouldn't be an issue.):\n");
                Assert.True(writeTimeoutOnce, "Hit testing race condition. [Never encountered a ReadTimeoutException.] (Shouldn't be an issue.):\n");
                Assert.True(unavailableOnce, "Hit testing race condition. [Never encountered an UnavailableException.] (Shouldn't be an issue.):\n");

                // TODO: Missing test to see if nodes were written to

            }
            catch (Exception e)
            {
                c.ErrorOut();
                throw e;
            }
            finally
            {
                resetCoordinators();
                c.Discard();
            }
        }

        /// <summary>
        ///  Tests DowngradingConsistencyRetryPolicy
        /// </summary>

        [TestMethod]
        [WorksForMe]
        public void downgradingConsistencyRetryPolicy()
        {
            var builder = Cluster.Builder().WithRetryPolicy(DowngradingConsistencyRetryPolicy.Instance);
            downgradingConsistencyRetryPolicy(builder);
        }

        /// <summary>
        ///  Tests DowngradingConsistencyRetryPolicy with LoggingRetryPolicy
        /// </summary>

        [TestMethod]
        [WorksForMe]
        public void downgradingConsistencyLoggingPolicy()
        {
            var builder = Cluster.Builder().WithRetryPolicy(new LoggingRetryPolicy(DowngradingConsistencyRetryPolicy.Instance));
            downgradingConsistencyRetryPolicy(builder);
        }

        /// <summary>
        ///  Tests DowngradingConsistencyRetryPolicy
        /// </summary>

        public void downgradingConsistencyRetryPolicy(Builder builder)
        {
            CCMBridge.CCMCluster c = CCMBridge.CCMCluster.Create(3, builder);
            createSchema(c.Session, 3);

            // FIXME: Race condition where the nodes are not fully up yet and assertQueried reports slightly different numbers
            Thread.Sleep(5000);
            try
            {
                init(c, 12, ConsistencyLevel.All);
                query(c, 12, ConsistencyLevel.All);

                assertQueried(CCMBridge.IP_PREFIX + "1", 4);
                assertQueried(CCMBridge.IP_PREFIX + "2", 4);
                assertQueried(CCMBridge.IP_PREFIX + "3", 4);

                resetCoordinators();
                c.CassandraCluster.ForceStop(2);
                TestUtils.waitForDownWithWait(CCMBridge.IP_PREFIX + "2", c.Cluster, 10);

                query(c, 12, ConsistencyLevel.All);

                assertQueried(CCMBridge.IP_PREFIX + "1", 6);
                assertQueried(CCMBridge.IP_PREFIX + "2", 0);
                assertQueried(CCMBridge.IP_PREFIX + "3", 6);

                resetCoordinators();
                c.CassandraCluster.ForceStop(1);
                TestUtils.waitForDownWithWait(CCMBridge.IP_PREFIX + "1", c.Cluster, 5);
                Thread.Sleep(5000);

                try
                {
                    query(c, 12, ConsistencyLevel.All);
                }
                catch (ReadTimeoutException e)
                {
                    //                    assertEquals("Cassandra timeout during read query at consistency TWO (2 responses were required but only 1 replica responded)", e.getMessage());
                }

                query(c, 12, ConsistencyLevel.Quorum);

                assertQueried(CCMBridge.IP_PREFIX + "1", 0);
                assertQueried(CCMBridge.IP_PREFIX + "2", 0);
                assertQueried(CCMBridge.IP_PREFIX + "3", 12);

                resetCoordinators();

                query(c, 12, ConsistencyLevel.Two);

                assertQueried(CCMBridge.IP_PREFIX + "1", 0);
                assertQueried(CCMBridge.IP_PREFIX + "2", 0);
                assertQueried(CCMBridge.IP_PREFIX + "3", 12);

                resetCoordinators();

                query(c, 12, ConsistencyLevel.One);

                assertQueried(CCMBridge.IP_PREFIX + "1", 0);
                assertQueried(CCMBridge.IP_PREFIX + "2", 0);
                assertQueried(CCMBridge.IP_PREFIX + "3", 12);

            }
            catch (Exception e)
            {
                c.ErrorOut();
                throw e;
            }
            finally
            {
                resetCoordinators();
                c.Discard();
            }
        }

        /*
         * Test the AlwaysIgnoreRetryPolicy with Logging enabled.
         */
        [TestMethod]
        [WorksForMe]
        public void alwaysIgnoreRetryPolicyTest()
        {
            var builder = Cluster.Builder().WithRetryPolicy(new LoggingRetryPolicy(AlwaysIgnoreRetryPolicy.Instance));
            CCMBridge.CCMCluster c = CCMBridge.CCMCluster.Create(2, builder);
            createSchema(c.Session);

            try
            {
                init(c, 12);
                query(c, 12);

                assertQueried(CCMBridge.IP_PREFIX + "1", 6);
                assertQueried(CCMBridge.IP_PREFIX + "2", 6);

                resetCoordinators();

                // Test failed reads
                c.CassandraCluster.ForceStop(2);
                for (int i = 0; i < 10; ++i)
                {
                    query(c, 12);
                }

                // A weak test to ensure that the nodes were contacted
                assertQueried(CCMBridge.IP_PREFIX + "1", 120);
                assertQueried(CCMBridge.IP_PREFIX + "2", 0);
                resetCoordinators();


                c.CassandraCluster.Start(2);
                TestUtils.waitFor(CCMBridge.IP_PREFIX + "2", c.Cluster, 30);

                // Test successful reads
                for (int i = 0; i < 10; ++i)
                {
                    query(c, 12);
                }

                // A weak test to ensure that the nodes were contacted
                assertQueriedAtLeast(CCMBridge.IP_PREFIX + "1", 1);
                assertQueriedAtLeast(CCMBridge.IP_PREFIX + "2", 1);
                resetCoordinators();


                // Test writes
                for (int i = 0; i < 100; ++i)
                {
                    init(c, 12);
                }

                // TODO: Missing test to see if nodes were written to


                // Test failed writes
                c.CassandraCluster.ForceStop(2);
                for (int i = 0; i < 100; ++i)
                {
                    init(c, 12);
                }

                // TODO: Missing test to see if nodes were written to

            }
            catch (Exception e)
            {
                c.ErrorOut();
                throw e;
            }
            finally
            {
                resetCoordinators();
                c.Discard();
            }
        }


        /*
         * Test the AlwaysIgnoreRetryPolicy with Logging enabled.
         */
        [TestMethod]
        [WorksForMe]
        public void alwaysRetryRetryPolicyTest()
        {
            var builder = Cluster.Builder().WithRetryPolicy(new LoggingRetryPolicy(AlwaysRetryRetryPolicy.Instance));
            CCMBridge.CCMCluster c = CCMBridge.CCMCluster.Create(2, builder);
            createSchema(c.Session);

            try
            {
                init(c, 12);
                query(c, 12);

                assertQueried(CCMBridge.IP_PREFIX + "1", 6);
                assertQueried(CCMBridge.IP_PREFIX + "2", 6);

                resetCoordinators();

                // Test failed reads
                c.CassandraCluster.ForceStop(2);

                Thread t1 = new Thread(() =>
                {
                    Console.WriteLine("1 Thread started");
                    try
                    {
                        query(c, 12);
                    }
                    catch (AsyncCallException)
                    {
                        Console.WriteLine("1 Thread async call broke");
                    } 
                    Console.WriteLine("1 Thread finished");
                });
                t1.Start();
                t1.Join(10000);
                if (t1.IsAlive)
                    t1.Interrupt();

                // A weak test to ensure that the nodes were contacted
                assertQueried(CCMBridge.IP_PREFIX + "1", 0);
                assertQueried(CCMBridge.IP_PREFIX + "2", 0);
                resetCoordinators();


                c.CassandraCluster.Start(2);
                TestUtils.waitFor(CCMBridge.IP_PREFIX + "2", c.Cluster, 30);

                // Test successful reads
                for (int i = 0; i < 10; ++i)
                {
                    try
                    {
                        query(c, 12);
                    }
                    catch (AsyncCallException)
                    {
                        Console.WriteLine("Main Thread async call broke");
                    }
                }

                // A weak test to ensure that the nodes were contacted
                assertQueriedAtLeast(CCMBridge.IP_PREFIX + "1", 1);
                assertQueriedAtLeast(CCMBridge.IP_PREFIX + "2", 1);
                resetCoordinators();


                // Test writes
                for (int i = 0; i < 100; ++i)
                {
                    init(c, 12);
                }

                // TODO: Missing test to see if nodes were written to


                // Test failed writes
                c.CassandraCluster.ForceStop(2);
                Thread t2 = new Thread(() =>
                {
                    Console.WriteLine("2 Thread started");
                    try
                    {
                        init(c, 12);
                        Assert.Fail();
                    }
                    catch (AsyncCallException e)
                    {
                        Console.WriteLine("2 Thread async call broke");
                    }
                    catch (NoHostAvailableException e) 
                    {
                        Console.WriteLine("2 Thread no host");
                    }
                    Console.WriteLine("2 Thread finished");
                });
                t2.Start();
                t2.Join(10000);
                if (t2.IsAlive)
                    t2.Interrupt();

                // TODO: Missing test to see if nodes were written to

            }
            catch (Exception e)
            {
                c.ErrorOut();
                throw e;
            }
            finally
            {
                resetCoordinators();
                c.Discard();
            }
        }
    }
}