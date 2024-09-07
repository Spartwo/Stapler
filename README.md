Stapler is a mod for Kerbal Space Program. It adds a construction mode to the editor that lets you re-parent parts without changing their position.

# Build Instructions

1. Open the solution.
2. Right click the solution in the Solution Explorer and click Restore NuGet Packages.

-

3. Double click Properties, go to Reference Paths, remove any existing reference paths and add your game folder.

OR

3. Edit Stapler.csproj.user to point to your game folder.

-

4. Attempt a build and everything should sort itself out. 

Recommend creating a symlink that points to `Stapler\GameData\Stapler` inside your `Kerbal Space Program\GameData` for faster development.