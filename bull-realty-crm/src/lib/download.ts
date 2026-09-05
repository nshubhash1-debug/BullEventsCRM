import { apiBlob } from "@/lib/api";

/**
 * Fetch a file from the API and hand it to the browser.
 *
 * A plain `<a href>` to the API cannot work: every route wants a bearer token
 * and a link carries no headers, so the tab would land on a 401. The file is
 * fetched over the authenticated channel and handed over as an object URL
 * instead.
 */
export async function downloadFile(path: string, filename: string) {
  const blob = await apiBlob(path);
  const url = URL.createObjectURL(blob);

  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  document.body.append(link);
  link.click();
  link.remove();

  // Revoked on the next turn of the event loop — immediately would race the
  // click in some browsers, and never would leak the blob for the session.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/**
 * Open a fetched document in a new tab.
 *
 * Same reason as above: the printable payslip is behind the bearer token, so
 * it has to be fetched and then shown from an object URL. The tab is opened
 * before the await so the click that triggered it is still what the browser
 * sees — open it afterwards and the popup blocker takes it.
 */
export async function openFile(path: string) {
  const tab = window.open("", "_blank");

  try {
    const blob = await apiBlob(path);
    const url = URL.createObjectURL(blob);

    if (tab) {
      tab.location.href = url;
    } else {
      // Blocked anyway — fall back to a download so the file is not simply lost.
      const link = document.createElement("a");
      link.href = url;
      link.download = "document.html";
      link.click();
    }

    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  } catch (error) {
    tab?.close();
    throw error;
  }
}
