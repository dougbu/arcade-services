// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityFrameworkCore.Triggers;
using Maestro.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace Maestro.Data
{
    public class BuildAssetRegistryContextFactory : IDesignTimeDbContextFactory<BuildAssetRegistryContext>
    {
        public BuildAssetRegistryContext CreateDbContext(string[] args)
        {
            var connectionString =
                @"Data Source=localhost\SQLEXPRESS;Initial Catalog=BuildAssetRegistry;Integrated Security=true";

            var envVarConnectionString = Environment.GetEnvironmentVariable("BUILD_ASSET_REGISTRY_DB_CONNECTION_STRING");
            if (!string.IsNullOrEmpty(envVarConnectionString))
            {
                Console.WriteLine("Using Connection String from environment.");
                connectionString = envVarConnectionString;
            }

            DbContextOptions options = new DbContextOptionsBuilder()
                .UseSqlServerWithRetry(connectionString, opts =>
                {
                    opts.CommandTimeout(30 * 60);
                })
                .Options;
            return new BuildAssetRegistryContext(
                new HostingEnvironment{EnvironmentName = Environments.Development},
                options);
        }
    }

    public class BuildAssetRegistryContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public BuildAssetRegistryContext(IHostEnvironment hostingEnvironment, DbContextOptions options) : base(
            options)
        {
            HostingEnvironment = hostingEnvironment;
        }

        public IHostEnvironment HostingEnvironment { get; }

        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetLocation> AssetLocations { get; set; }
        public DbSet<Build> Builds { get; set; }
        public DbSet<BuildChannel> BuildChannels { get; set; }
        public DbSet<BuildDependency> BuildDependencies { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<DefaultChannel> DefaultChannels { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionUpdate> SubscriptionUpdates { get; set; }
        public DbSet<Repository> Repositories { get; set; }
        public DbSet<RepositoryBranch> RepositoryBranches { get; set; }
        public DbSet<RepositoryBranchUpdate> RepositoryBranchUpdates { get; set; }
        public DbSet<DependencyFlowEvent> DependencyFlowEvents { get; set; }
        public DbSet<GoalTime> GoalTime { get; set; }
        public DbSet<LongestBuildPath> LongestBuildPaths { get; set; }

        public virtual IQueryable<RepositoryBranchUpdateHistoryEntry> RepositoryBranchUpdateHistory => RepositoryBranchUpdates
            .TemporalAll()
            .Select(
                u => new RepositoryBranchUpdateHistoryEntry
                {
                    Repository = u.RepositoryName,
                    Branch = u.BranchName,
                    Action = u.Action,
                    Success = u.Success,
                    ErrorMessage = u.ErrorMessage,
                    Method = u.Method,
                    Arguments = u.Arguments,
                    Timestamp = EF.Property<DateTime>(u, "SysStartTime")
                });

        public virtual IQueryable<SubscriptionUpdateHistoryEntry> SubscriptionUpdateHistory => SubscriptionUpdates
            .TemporalAll()
            .Select(
                u => new SubscriptionUpdateHistoryEntry
                {
                    SubscriptionId = u.SubscriptionId,
                    Action = u.Action,
                    Success = u.Success,
                    ErrorMessage = u.ErrorMessage,
                    Method = u.Method,
                    Arguments = u.Arguments,
                    Timestamp = EF.Property<DateTime>(u, "SysStartTime")
                });

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = new CancellationToken())
        {
            return this.SaveChangesWithTriggersAsync(
                base.SaveChangesAsync,
                acceptAllChangesOnSuccess,
                cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Asset>()
                .HasIndex(a => new {a.Name, a.Version});

            builder.Entity<Channel>().HasIndex(c => c.Name).IsUnique();

            builder.Entity<BuildChannel>()
            .HasKey(
                bc => new
                {
                    bc.BuildId,
                    bc.ChannelId
                });

            builder.Entity<BuildChannel>()
                .HasOne(bc => bc.Build)
                .WithMany(b => b.BuildChannels)
                .HasForeignKey(bc => bc.BuildId);

            builder.Entity<BuildChannel>()
                .HasOne(bc => bc.Channel)
                .WithMany(c => c.BuildChannels)
                .HasForeignKey(bc => bc.ChannelId);

            builder.Entity<BuildDependency>()
                .HasKey(d => new {d.BuildId, d.DependentBuildId});

            builder.Entity<BuildDependency>()
                .HasOne(d => d.Build)
                .WithMany()
                .HasForeignKey(d => d.BuildId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BuildDependency>()
                .HasOne(d => d.DependentBuild)
                .WithMany()
                .HasForeignKey(d => d.DependentBuildId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ApplicationUserPersonalAccessToken>()
                .HasIndex(
                    t => new
                    {
                        t.ApplicationUserId,
                        t.Name
                    })
                .IsUnique();

            builder.Entity<DefaultChannel>()
                .HasIndex(
                    dc => new
                    {
                        dc.Repository,
                        dc.Branch,
                        dc.ChannelId
                    })
                .IsUnique();
            
            builder.Entity<SubscriptionUpdate>().Property(typeof(DateTime), "SysStartTime").HasColumnType("datetime2");
            builder.Entity<SubscriptionUpdate>().Property(typeof(DateTime), "SysEndTime").HasColumnType("datetime2");

            builder.Entity<SubscriptionUpdate>()
                .ToTable(b =>
                {
                    b.IsTemporal(t =>
                    {
                        t.HasPeriodStart("SysStartTime").HasColumnName("SysStartTime");
                        t.HasPeriodEnd("SysEndTime").HasColumnName("SysEndTime");
                        t.UseHistoryTable(nameof(SubscriptionUpdateHistory));
                    });
                })
                .HasOne(su => su.Subscription)
                .WithOne()
                .HasForeignKey<SubscriptionUpdate>(su => su.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<SubscriptionUpdateHistory>().ToTable(nameof(SubscriptionUpdateHistory));
            builder.Entity<SubscriptionUpdateHistory>().HasNoKey();
            builder.Entity<SubscriptionUpdateHistory>().Property(typeof(DateTime), "SysStartTime").HasColumnType("datetime2");
            builder.Entity<SubscriptionUpdateHistory>().Property(typeof(DateTime), "SysEndTime").HasColumnType("datetime2");
            builder.Entity<SubscriptionUpdateHistory>().HasIndex("SysEndTime", "SysStartTime").IsClustered();
            builder.Entity<SubscriptionUpdateHistory>().HasIndex("SubscriptionId", "SysEndTime", "SysStartTime");

            builder.Entity<Repository>().HasKey(r => new {r.RepositoryName});

            builder.Entity<RepositoryBranch>()
                .HasKey(
                    rb => new
                    {
                        rb.RepositoryName,
                        rb.BranchName
                    });

            builder.Entity<RepositoryBranch>()
                .HasOne(rb => rb.Repository)
                .WithMany(r => r.Branches)
                .HasForeignKey(rb => new {rb.RepositoryName});

            builder.Entity<RepositoryBranchUpdate>()
                .HasKey(
                    ru => new
                    {
                        ru.RepositoryName,
                        ru.BranchName
                    });

            builder.Entity<RepositoryBranchUpdate>().Property(typeof(DateTime), "SysStartTime").HasColumnType("datetime2");
            builder.Entity<RepositoryBranchUpdate>().Property(typeof(DateTime), "SysEndTime").HasColumnType("datetime2");
            builder.Entity<RepositoryBranchUpdate>()
                .ToTable(b =>
                {
                    b.IsTemporal(t =>
                    {
                        t.HasPeriodStart("SysStartTime").HasColumnName("SysStartTime");
                        t.HasPeriodEnd("SysEndTime").HasColumnName("SysEndTime");
                        t.UseHistoryTable(nameof(RepositoryBranchUpdateHistory));
                    });
                })
                .HasOne(ru => ru.RepositoryBranch)
                .WithOne()
                .HasForeignKey<RepositoryBranchUpdate>(
                    ru => new
                    {
                        ru.RepositoryName,
                        ru.BranchName
                    })
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RepositoryBranchUpdateHistory>().ToTable(nameof(RepositoryBranchUpdateHistory));
            builder.Entity<RepositoryBranchUpdateHistory>()
                .HasNoKey();

            builder.Entity<RepositoryBranchUpdateHistory>().Property(typeof(DateTime), "SysStartTime").HasColumnType("datetime2");
            builder.Entity<RepositoryBranchUpdateHistory>().Property(typeof(DateTime), "SysEndTime").HasColumnType("datetime2");
            builder.Entity<RepositoryBranchUpdateHistory>().HasIndex("SysEndTime", "SysStartTime").IsClustered();
            builder.Entity<RepositoryBranchUpdateHistory>()
                .HasIndex("RepositoryName", "BranchName", "SysEndTime", "SysStartTime");

            builder.Entity<GoalTime>()
                .HasKey(
                    gt => new
                    {
                        gt.DefinitionId,
                        gt.ChannelId
                    });

            builder.Entity<GoalTime>()
                .HasOne(gt => gt.Channel)
                .WithMany()
                .HasForeignKey(gt => gt.ChannelId);

            builder.HasDbFunction(() => JsonExtensions.JsonValue("", ""))
                .HasTranslation(args => new SqlFunctionExpression(
                    "JSON_VALUE",
                    args,
                    nullable: false,
                    argumentsPropagateNullability: args.Select(_ => false),
                    typeof(string),
                    null
                ));
        }

        public virtual Task<long> GetInstallationId(string repositoryUrl)
        {
            return Repositories.Where(r => r.RepositoryName == repositoryUrl)
                .Select(r => r.InstallationId)
                .FirstOrDefaultAsync();
        }

        public async Task<IList<Build>> GetBuildGraphAsync(int buildId)
        {
            var dependencyEntity = Model.FindEntityType(typeof(BuildDependency));
            // The "new" code is much more complicated and might not return what we need, suppress the warning
#pragma warning disable CS0618
            var buildIdColumnName = dependencyEntity.FindProperty(nameof(BuildDependency.BuildId)).GetColumnName();
            var dependencyIdColumnName = dependencyEntity.FindProperty(nameof(BuildDependency.DependentBuildId)).GetColumnName();
            var isProductColumnName = dependencyEntity.FindProperty(nameof(BuildDependency.IsProduct)).GetColumnName();
            var timeToInclusionInMinutesColumnName = dependencyEntity.FindProperty(nameof(BuildDependency.TimeToInclusionInMinutes)).GetColumnName();
#pragma warning restore CS0618
            var edgeTable = dependencyEntity.GetTableName();

            var edges = BuildDependencies.FromSqlRaw($@"
WITH traverse AS (
        SELECT
            {buildIdColumnName},
            {dependencyIdColumnName},
            {isProductColumnName},
            {timeToInclusionInMinutesColumnName},
            0 as Depth
        from {edgeTable}
        WHERE {buildIdColumnName} = @id
    UNION ALL
        SELECT
            {edgeTable}.{buildIdColumnName},
            {edgeTable}.{dependencyIdColumnName},
            {edgeTable}.{isProductColumnName},
            {edgeTable}.{timeToInclusionInMinutesColumnName},
            traverse.Depth + 1
        FROM {edgeTable}
        INNER JOIN traverse
        ON {edgeTable}.{buildIdColumnName} = traverse.{dependencyIdColumnName}
        WHERE traverse.{isProductColumnName} = 1 -- The thing we previously traversed was a product dependency
            AND traverse.Depth < 10 -- Don't load all the way back because of incorrect isProduct columns
)
SELECT DISTINCT {buildIdColumnName}, {dependencyIdColumnName}, {isProductColumnName}, {timeToInclusionInMinutesColumnName}
FROM traverse;",
               new SqlParameter("id", buildId));

            List<BuildDependency> things = await edges.ToListAsync();
            var buildIds = new HashSet<int>(things.SelectMany(t => new[] { t.BuildId, t.DependentBuildId }));

            buildIds.Add(buildId); // Make sure we always include the requested build, even if it has no edges.

            IQueryable<Build> builds = from build in Builds
                                       where buildIds.Contains(build.Id)
                                       select build;

            Dictionary<int, Build> dict = await builds.ToDictionaryAsync(b => b.Id,
               b =>
               {
                   b.DependentBuildIds = new List<BuildDependency>();
                   return b;
               });

            foreach (var edge in things)
            {
                dict[edge.BuildId].DependentBuildIds.Add(edge);
            }

            // Gather subscriptions used by this build.
            Build primaryBuild = Builds.First(b => b.Id == buildId);

            var validSubscriptions = await Subscriptions.Where(s =>
                    (s.TargetRepository == primaryBuild.AzureDevOpsRepository ||
                        s.TargetRepository == primaryBuild.GitHubRepository) &&
                    (s.TargetBranch == primaryBuild.AzureDevOpsBranch ||
                        s.TargetBranch == primaryBuild.GitHubBranch ||
                        "refs/heads/" + s.TargetBranch == primaryBuild.AzureDevOpsBranch ||
                        "refs/heads/" + s.TargetBranch == primaryBuild.GitHubBranch))
                .ToListAsync();

            // Use the subscriptions to determine what channels are relevant for this build, so just grab the unique channel ID's from valid suscriptions
            var channelIds = validSubscriptions.GroupBy(x => x.ChannelId).Select(y => y.First()).Select(s => s.ChannelId);

            // Acquire list of builds in valid channels
            var channelBuildIds = await BuildChannels.Where(b => channelIds.Any(c => c == b.ChannelId)).Select(s => s.BuildId).ToListAsync();
            var possibleBuilds = await Builds.Where(b => channelBuildIds.Any(c => c == b.Id)).ToListAsync();

            // Calculate total number of builds that are newer.
            foreach (var id in dict.Keys)
            {
                var build = dict[id];
                // Get newer builds data for this channel.
                var newer = possibleBuilds.Where(b => b.GitHubRepository == build.GitHubRepository &&
                                                    b.AzureDevOpsRepository == build.AzureDevOpsRepository &&
                                                    b.DateProduced > build.DateProduced);
                dict[id].Staleness = newer.Count();
            }
            return dict.Values.ToList();
        }

        public bool IsProductDependency(
            string sourceRepositoryName,
            string sourceBranchName,
            string targetRepositoryName,
            string targetBranchName)
        {
            // Take latest target builds ordered by DateProduced
            var latestTargetBuildsQuery = Builds
                .Where(b =>
                    (b.GitHubRepository == targetRepositoryName && b.GitHubBranch == targetBranchName)
                    || (b.AzureDevOpsRepository == targetRepositoryName && b.AzureDevOpsBranch == targetBranchName))
                .OrderByDescending(b => b.DateProduced);

            // For each build return its Id and HasProductDependencies flag saying
            // if there are any build dependencies in this build that come from source
            // and have IsProduct flag set.
            var buildDependenciesQuery = latestTargetBuildsQuery
                .Select(b => new
                {
                    Id = b.Id,
                    HasProductDependencies = BuildDependencies
                        .Any(d =>
                            ((d.DependentBuild.GitHubRepository == sourceRepositoryName
                                    && d.DependentBuild.GitHubBranch == sourceBranchName)
                                || (d.DependentBuild.AzureDevOpsRepository == sourceRepositoryName
                                    && d.DependentBuild.AzureDevOpsBranch == sourceBranchName))
                            && d.Build == b
                            && d.IsProduct)
                });

            // Take the most recent build. Can be null if there are no target builds yet.
            var latestBuild = buildDependenciesQuery.FirstOrDefault();

            return latestBuild?.HasProductDependencies ?? false;
        }
    }

    public class SubscriptionUpdateHistoryEntry : UpdateHistoryEntry
    {
        public Guid SubscriptionId { get; set; }
    }

    public class RepositoryBranchUpdateHistoryEntry : UpdateHistoryEntry
    {
        public string Repository { get; set; }
        public string Branch { get; set; }
    }

    public class UpdateHistoryEntry
    {
        public string Action { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Method { get; set; }
        public string Arguments { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
