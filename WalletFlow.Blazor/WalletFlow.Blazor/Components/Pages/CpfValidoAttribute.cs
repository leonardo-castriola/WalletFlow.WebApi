using System.ComponentModel.DataAnnotations;

namespace WalletFlow.Blazor.Components.Pages;

public sealed class CpfValidoAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        var cpf = value?.ToString();
        if (string.IsNullOrWhiteSpace(cpf)) return true;
        var digits = cpf.Where(char.IsDigit).Select(c => c - '0').ToArray();
        if (digits.Length != 11 || digits.All(digit => digit == digits[0])) return false;

        static int CalculateDigit(int[] numbers, int size, int initialWeight)
        {
            var sum = 0;
            for (var index = 0; index < size; index++)
                sum += numbers[index] * (initialWeight - index);
            var remainder = sum % 11;
            return remainder < 2 ? 0 : 11 - remainder;
        }

        return digits[9] == CalculateDigit(digits, 9, 10)
               && digits[10] == CalculateDigit(digits, 10, 11);
    }
}
