using System;
using System.Data.Entity;
using System.Data.Entity.SqlServer;

namespace GmrLib.Models
{
    using System.Data.Entity.Infrastructure;
    using System.Runtime.Remoting.Messaging;

    public class GmrDbConfiguration : DbConfiguration
    {
        public GmrDbConfiguration()
        {
            SetExecutionStrategy(
                "System.Data.SqlClient",
                () => SuspendExecutionStrategy
                    ? (IDbExecutionStrategy)new DefaultExecutionStrategy()
                    : new SqlAzureExecutionStrategy(2, TimeSpan.FromSeconds(30)));
        }

        public static bool SuspendExecutionStrategy
        {
            get
            {
                return (bool?)CallContext.LogicalGetData("SuspendExecutionStrategy") ?? false;
            }
            set
            {
                CallContext.LogicalSetData("SuspendExecutionStrategy", value);
            }
        }
    }
}