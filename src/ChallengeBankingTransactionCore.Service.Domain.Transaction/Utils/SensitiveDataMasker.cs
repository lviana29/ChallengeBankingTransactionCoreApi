namespace ChallengeBankingTransactionCore.Service.Domain.Transaction.Utils {
    public static class SensitiveDataMasker {
        public static string MaskAccountNumber(string? accountNumber) {
            if (string.IsNullOrWhiteSpace(accountNumber)) {
                return "***";
            }

            var trimmed = accountNumber.Trim();
            if (trimmed.Length <= 3) {
                return "***";
            }

            var hyphenIndex = trimmed.IndexOf('-');
            if (hyphenIndex > 0 && hyphenIndex < trimmed.Length - 1) {
                var prefix = trimmed.Length >= 2 && hyphenIndex >= 2
                    ? trimmed[..2]
                    : trimmed[..1];
                var suffix = trimmed[hyphenIndex..];
                return $"{prefix}***{suffix}";
            }

            return $"{trimmed[..2]}***{trimmed[^1]}";
        }
    }
}
