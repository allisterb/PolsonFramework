namespace Polson;

using System;
using System.IO;

/// <summary>
/// Resolves a script-supplied path against the project directory, and refuses one that escapes it.
/// </summary>
/// <remarks>
/// Shared by every call that takes a path from a script — <c>outFile</c>, <c>outSvg</c> and
/// <c>Skia.Image.load</c> — so that one relative path means one thing whichever call receives it.
/// It did not, once: <c>outFile: 'artifacts/x.webp'</c> resolved against the project while
/// <c>Skia.Image.load('artifacts/x.webp')</c> resolved against the server's working directory and
/// reported the file missing. An agent hit that, worked around it, and only mentioned it in passing.
/// <para>
/// Containment applies to reads as well as writes. Reading is the milder of the two, but a harness
/// whose whole premise is that the agent cannot reach outside its directory does not get to make an
/// exception for the direction that happens to be less alarming.
/// </para>
/// </remarks>
public static class ProjectPath
{
    #region Methods
    /// <summary>
    /// Returns the absolute path <paramref name="path"/> names inside <paramref name="projectRoot"/>.
    /// </summary>
    /// <param name="projectRoot">The project directory. When null or empty nothing is contained,
    /// which is the ad-hoc case: a server started without <c>--project-dir</c> has no project to be
    /// inside of.</param>
    /// <param name="path">The path as the script wrote it.</param>
    /// <param name="parameterName">Reported as the offending parameter.</param>
    /// <param name="action">The verb used in the message — "Write" or "Read".</param>
    /// <exception cref="ArgumentException">The path resolves outside the project.</exception>
    public static string Resolve(string? projectRoot, string path, string parameterName, string action)
    {
        var full = string.IsNullOrEmpty(projectRoot)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(projectRoot, path));

        if (string.IsNullOrEmpty(projectRoot)) return full;

        // Normalised the same way `full` was, so a root written with forward slashes or a trailing
        // separator is compared like for like rather than refusing every path inside it.
        projectRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));

        // Case-insensitive only where the filesystem is: on Linux "/a" and "/A" are different
        // directories, and ignoring case there would accept an escape as if it were contained.
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // The trailing separator stops "C:\proj" from matching a sibling "C:\project-two".
        var contained = full.Equals(projectRoot, comparison)
            || full.StartsWith(projectRoot + Path.DirectorySeparatorChar, comparison);

        if (!contained)
        {
            throw new ArgumentException(
                $"'{path}' resolves to '{full}', which is outside this project's directory " +
                $"('{projectRoot}'). {action} a path inside the project, such as 'artifacts/stage1.webp'.",
                parameterName);
        }

        return full;
    }
    #endregion
}
