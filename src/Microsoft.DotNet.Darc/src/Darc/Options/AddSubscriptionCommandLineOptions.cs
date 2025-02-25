// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;
using System.Collections.Generic;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("add-subscription", HelpText = "Add a new subscription.")]
    class AddSubscriptionCommandLineOptions : CommandLineOptions
    {
        [Option("channel", HelpText = "Name of channel to pull from.")]
        public string Channel { get; set; }

        [Option("source-repo", HelpText = "Source repository for the subscription.")]
        public string SourceRepository { get; set; }

        [Option("target-repo", HelpText = "Target repository for the subscription.")]
        public string TargetRepository { get; set; }

        [Option("target-branch", HelpText = "Target branch for the subscription.")]
        public string TargetBranch { get; set; }

        [Option("update-frequency", HelpText = "Frequency of updates. Valid values are: 'none', 'everyDay', 'everyBuild', 'twiceDaily', or 'everyWeek'.")]
        public string UpdateFrequency { get; set; }

        [Option("batchable", HelpText = "Make subscription batchable.")]
        public bool Batchable { get; set; }

        [Option("standard-automerge", HelpText = "Use standard auto-merge policies. GitHub ignores WIP, license/cla and auto-merge.config.enforce checks, " +
            "Azure DevOps ignores comment, reviewer and work item linking. Both will not auto-merge if changes are requested.")]
        public bool StandardAutoMergePolicies { get; set; }

        [Option("all-checks-passed", HelpText = "PR is automatically merged if there is at least one check, and all checks have passed. " +
            "Optionally provide a comma-separated list of ignored check with --ignore-checks.")]
        public bool AllChecksSuccessfulMergePolicy { get; set; }

        [Option("ignore-checks", Separator = ',', HelpText = "For use with --all-checks-passed. A set of checks that are ignored.")]
        public IEnumerable<string> IgnoreChecks { get; set; }

        [Option("no-requested-changes", HelpText = "PR is not merged if there are changes requested or the PR has been rejected.")]
        public bool NoRequestedChangesMergePolicy { get; set; }

        [Option("no-downgrades", HelpText = "PR is not merged if there are version downgrades.")]
        public bool DontAutomergeDowngradesMergePolicy { get; set; }

        [Option('q', "quiet", HelpText = "Non-interactive mode (requires all elements to be passed on the command line).")]
        public bool Quiet { get; set; }

        [Option("read-stdin", HelpText = "Interactive mode style (YAML), but read input from stdin. Implies -q")]
        public bool ReadStandardIn { get; set; }

        [Option("trigger", SetName = "trigger", HelpText = "Automatically trigger the subscription on creation.")]
        public bool TriggerOnCreate { get; set; }

        [Option("no-trigger", SetName = "notrigger", HelpText = "Do not trigger the subscription on creation.")]
        public bool NoTriggerOnCreate { get; set; }

        [Option("failure-notification-tags", HelpText = "Semicolon-delineated list of GitHub tags (GitHub login or GitHub team) to notify in the case of non-batched subscription pull-request policy failure.  Users must be publicly a member of the Microsoft org.", Default = "")]
        public string PullRequestFailureNotificationTags { get; set; }

        public override Operation GetOperation()
        {
            return new AddSubscriptionOperation(this);
        }
    }
}
