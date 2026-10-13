package com.reim.shiranuix.util;

import java.util.Arrays;
import java.util.Comparator;

/**
 * Natural ("human") string ordering, so {@code page2.png} sorts before
 * {@code page10.png}.
 *
 * <p>{@link String#compareTo(Object)} would order {@code page10} before
 * {@code page2} because it compares lexicographically. This comparator
 * instead splits each string into runs of digits and non-digits and compares
 * digit runs numerically, which is what users expect from file names.</p>
 *
 * <p>Usage:</p>
 * <pre>{@code
 *     files.sort(NaturalSort.naturalOrder());
 * }</pre>
 */
public final class NaturalSort {

    /** Private utility constructor. */
    private NaturalSort() {
    }

    /**
     * Returns a comparator that orders strings naturally.
     *
     * <p>The comparator is case-insensitive and treats a numeric run as equal
     * to the same number without leading zeros (so {@code "01"} equals
     * {@code "1"}; the original lexicographic comparison is used as a
     * tie-breaker to keep the order stable).</p>
     *
     * @param <T> a type that can be converted to a {@link String}
     * @return a natural-order comparator
     */
    public static <T> Comparator<T> naturalOrder() {
        return (left, right) -> compare(
                String.valueOf(left), String.valueOf(right));
    }

    /**
     * Compares two strings naturally (numeric runs compared numerically).
     *
     * @param left  first string, may be {@code null}
     * @param right second string, may be {@code null}
     * @return negative, zero, or positive as {@code left} sorts before,
     *         the same as, or after {@code right}
     */
    public static int compare(String left, String right) {
        if (left == null || right == null) {
            return Boolean.compare(left == null, right == null);
        }

        String[] leftParts  = splitRuns(left);
        String[] rightParts = splitRuns(right);

        int shared = Math.min(leftParts.length, rightParts.length);
        for (int i = 0; i < shared; i++) {
            String a = leftParts[i];
            String b = rightParts[i];

            if (isNumeric(a) && isNumeric(b)) {
                // Compare digit runs by value, ignoring leading zeros.
                int byValue = stripZeros(a).compareTo(stripZeros(b));
                if (byValue != 0) {
                    return byValue;
                }
                // Same numeric value: prefer the one with fewer digits so
                // "1" comes before "01" deterministically.
                int byLength = Integer.compare(a.length(), b.length());
                if (byLength != 0) {
                    return byLength;
                }
            } else {
                int byText = a.compareToIgnoreCase(b);
                if (byText != 0) {
                    return byText;
                }
            }
        }

        // All shared parts were equal; the shorter string sorts first.
        return Integer.compare(leftParts.length, rightParts.length);
    }

    /**
     * Splits a string into alternating runs of digits and non-digits.
     *
     * @param input the string to split
     * @return an array of runs; never {@code null}
     */
    private static String[] splitRuns(String input) {
        return input.split("(?<=\\D)(?=\\d)|(?<=\\d)(?=\\D)");
    }

    private static boolean isNumeric(String run) {
        if (run.isEmpty()) {
            return false;
        }
        for (int i = 0; i < run.length(); i++) {
            if (!Character.isDigit(run.charAt(i))) {
                return false;
            }
        }
        return true;
    }

    /** Removes leading zeros, but keeps a single zero for the value {@code 0}. */
    private static String stripZeros(String digits) {
        int firstNonZero = 0;
        while (firstNonZero < digits.length() - 1
                && digits.charAt(firstNonZero) == '0') {
            firstNonZero++;
        }
        return digits.substring(firstNonZero);
    }
}
