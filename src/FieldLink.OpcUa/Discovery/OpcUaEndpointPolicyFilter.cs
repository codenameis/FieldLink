using System;
using System.Collections.Generic;
using Opc.Ua;

namespace FieldLink.OpcUa.Discovery;

internal static class OpcUaEndpointPolicyFilter
{
    private const int PolicyNotFoundRank = -1;

    private static readonly string[] IntrinsicStrongPolicyOrder =
    {
        SecurityPolicies.Aes256_Sha256_RsaPss,
        SecurityPolicies.Aes128_Sha256_RsaOaep,
        SecurityPolicies.Basic256Sha256,
        SecurityPolicies.ECC_nistP384,
        SecurityPolicies.ECC_nistP256,
        SecurityPolicies.ECC_brainpoolP384r1,
        SecurityPolicies.ECC_brainpoolP256r1,
        SecurityPolicies.ECC_curve448,
        SecurityPolicies.ECC_curve25519
    };

    public static bool IsCompatibleEndpoint(
        EndpointDescription candidate,
        OpcUaClientOptions options,
        out int policyRank,
        out UserTokenPolicyCollection? approvedTokens)
    {
        policyRank = int.MaxValue;
        approvedTokens = null;

        if (!string.Equals(candidate.TransportProfileUri, Profiles.UaTcpTransport, StringComparison.Ordinal))
        {
            return false;
        }

        if (candidate.SecurityMode != options.MessageSecurityMode)
        {
            return false;
        }

        bool isNoneMode = candidate.SecurityMode == MessageSecurityMode.None;
        bool isNonePolicy = string.Equals(candidate.SecurityPolicyUri, SecurityPolicies.None, StringComparison.Ordinal);
        if (isNoneMode != isNonePolicy)
        {
            return false;
        }

        policyRank = FindPolicyRank(candidate.SecurityPolicyUri, options.AllowedSecurityPolicies);
        if (policyRank < 0)
        {
            return false;
        }

        approvedTokens = SelectApprovedUserTokenPolicies(candidate, options);
        return approvedTokens is { Count: > 0 };
    }

    private static int FindPolicyRank(string? policyUri, IReadOnlyList<string> allowedPolicies)
    {
        for (int i = 0; i < allowedPolicies.Count; i++)
        {
            if (string.Equals(allowedPolicies[i], policyUri, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return PolicyNotFoundRank;
    }

    private static UserTokenPolicyCollection? SelectApprovedUserTokenPolicies(
        EndpointDescription candidate,
        OpcUaClientOptions options)
    {
        if (candidate.UserIdentityTokens is null)
        {
            return null;
        }

        if (options.UserIdentity.IdentityType == OpcUaUserIdentityType.Anonymous)
        {
            UserTokenPolicyCollection anonymousPolicies = new();
            for (int i = 0; i < candidate.UserIdentityTokens.Count; i++)
            {
                UserTokenPolicy? policy = candidate.UserIdentityTokens[i];
                if (policy is not null && policy.TokenType == UserTokenType.Anonymous)
                {
                    anonymousPolicies.Add((UserTokenPolicy)policy.Clone());
                }
            }

            return anonymousPolicies.Count > 0 ? anonymousPolicies : null;
        }

        return SelectApprovedUserNamePolicies(candidate, options);
    }

    private static UserTokenPolicyCollection? SelectApprovedUserNamePolicies(
        EndpointDescription candidate,
        OpcUaClientOptions options)
    {
        List<RankedTokenPolicy> encryptedPolicies = new();
        UserTokenPolicyCollection fallbackUnencryptedPolicies = new();

        for (int i = 0; i < candidate.UserIdentityTokens.Count; i++)
        {
            UserTokenPolicy? policy = candidate.UserIdentityTokens[i];
            if (policy is null || policy.TokenType != UserTokenType.UserName)
            {
                continue;
            }

            string? effectivePolicyUri = !string.IsNullOrEmpty(policy.SecurityPolicyUri)
                ? policy.SecurityPolicyUri
                : candidate.SecurityPolicyUri;

            if (IsUnencryptedTokenPolicy(effectivePolicyUri))
            {
                if (candidate.SecurityMode == MessageSecurityMode.SignAndEncrypt ||
                    options.AllowInsecureUserNameOverUnencryptedChannel)
                {
                    fallbackUnencryptedPolicies.Add((UserTokenPolicy)policy.Clone());
                }

                continue;
            }

            if (IsSha1SecurityPolicy(effectivePolicyUri))
            {
                bool explicitlyAllowedOverEncryptedChannel =
                    candidate.SecurityMode == MessageSecurityMode.SignAndEncrypt &&
                    FindPolicyRank(effectivePolicyUri, options.AllowedSecurityPolicies) >= 0;
                if (!explicitlyAllowedOverEncryptedChannel)
                {
                    continue;
                }
            }

            int clientRank = FindPolicyRank(effectivePolicyUri, options.AllowedSecurityPolicies);
            int strengthRank = FindIntrinsicStrongPolicyRank(effectivePolicyUri);
            if (clientRank < 0 && strengthRank < 0)
            {
                continue;
            }

            encryptedPolicies.Add(new RankedTokenPolicy(
                (UserTokenPolicy)policy.Clone(),
                clientRank >= 0 ? clientRank : int.MaxValue,
                strengthRank >= 0 ? strengthRank : int.MaxValue));
        }

        if (encryptedPolicies.Count > 0)
        {
            encryptedPolicies.Sort(static (left, right) =>
            {
                int clientCompare = left.ClientRank.CompareTo(right.ClientRank);
                return clientCompare != 0
                    ? clientCompare
                    : left.StrengthRank.CompareTo(right.StrengthRank);
            });

            UserTokenPolicyCollection approvedEncrypted = new();
            for (int i = 0; i < encryptedPolicies.Count; i++)
            {
                approvedEncrypted.Add(encryptedPolicies[i].Policy);
            }

            return approvedEncrypted;
        }

        return fallbackUnencryptedPolicies.Count > 0 ? fallbackUnencryptedPolicies : null;
    }

    private static bool IsUnencryptedTokenPolicy(string? tokenSecurityPolicyUri) =>
        string.IsNullOrEmpty(tokenSecurityPolicyUri) ||
        string.Equals(tokenSecurityPolicyUri, SecurityPolicies.None, StringComparison.Ordinal);

    private static bool IsSha1SecurityPolicy(string? securityPolicyUri) =>
        string.Equals(securityPolicyUri, SecurityPolicies.Basic128Rsa15, StringComparison.Ordinal) ||
        string.Equals(securityPolicyUri, SecurityPolicies.Basic256, StringComparison.Ordinal);

    private static int FindIntrinsicStrongPolicyRank(string? securityPolicyUri)
    {
        for (int i = 0; i < IntrinsicStrongPolicyOrder.Length; i++)
        {
            if (string.Equals(IntrinsicStrongPolicyOrder[i], securityPolicyUri, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return PolicyNotFoundRank;
    }

    private readonly struct RankedTokenPolicy
    {
        public RankedTokenPolicy(UserTokenPolicy policy, int clientRank, int strengthRank)
        {
            Policy = policy;
            ClientRank = clientRank;
            StrengthRank = strengthRank;
        }

        public UserTokenPolicy Policy { get; }

        public int ClientRank { get; }

        public int StrengthRank { get; }
    }
}
