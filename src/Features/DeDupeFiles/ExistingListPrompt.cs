using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// Asks what to do about a checksum list that is already in the folder: refresh it, or trust it and
/// go straight to the analysis. Returns <c>null</c> when the user backs out.
/// </summary>
internal static class ExistingListPrompt
{
    public static ExistingListAction? Ask(IWin32Window? owner, string listPath, int entries)
    {
        var saved = entries.ToString("N0", CultureInfo.InvariantCulture);
        try
        {
            return AskWithTaskDialog(owner, listPath, saved);
        }
        catch (Exception)
        {
            // Older shells / no visual styles: fall back to a plain message box.
            return AskWithMessageBox(listPath, saved);
        }
    }

    private static ExistingListAction? AskWithTaskDialog(IWin32Window? owner, string listPath, string saved)
    {
        var update = new TaskDialogCommandLinkButton(
            "&Update the list",
            "Re-scan the folder. Files that have not changed keep their saved checksum, so this is quick.");
        var useAsIs = new TaskDialogCommandLinkButton(
            "&Use the list as it is",
            saved + " checksum(s) are already saved. Skip scanning and go straight to the analysis.");
        var cancel = new TaskDialogCommandLinkButton("Cancel", "Do nothing for now.");

        var page = new TaskDialogPage
        {
            Caption = "Checksum list already exists",
            Heading = "This folder already has a checksum list",
            Text = listPath,
            Icon = TaskDialogIcon.Information,
            AllowCancel = true,
            Buttons = [update, useAsIs, cancel]
        };

        var clicked = owner is null
            ? TaskDialog.ShowDialog(page)
            : TaskDialog.ShowDialog(owner, page);

        if (clicked == update)
        {
            return ExistingListAction.Update;
        }
        return clicked == useAsIs ? ExistingListAction.UseAsIs : null;
    }

    private static ExistingListAction? AskWithMessageBox(string listPath, string saved)
    {
        var answer = MessageBox.Show(
            "This folder already has a checksum list with " + saved + " entry(ies):\n" + listPath
            + "\n\nYes  - update it (re-scan; unchanged files keep their checksum)\n"
            + "No   - use it as it is (skip scanning)\n"
            + "Cancel - do nothing",
            "Checksum list already exists",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Information);

        return answer switch
        {
            DialogResult.Yes => ExistingListAction.Update,
            DialogResult.No => ExistingListAction.UseAsIs,
            _ => null
        };
    }
}
