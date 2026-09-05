"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import {
  publicApi,
  PublicQuotation,
  formatRupees,
  formatPercent,
  formatIndian,
} from "@/lib/inventory-api";

export default function PublicQuotationPage() {
  const params = useParams();
  const token = params.token as string;

  const [quote, setQuote] = React.useState<PublicQuotation | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [comment, setComment] = React.useState("");
  const [responding, setResponding] = React.useState(false);
  const [successMsg, setSuccessMsg] = React.useState("");
  const [showCommentBox, setShowCommentBox] = React.useState<"Accepted" | "Rejected" | null>(null);

  React.useEffect(() => {
    if (!token) return;
    publicApi
      .quotation(token)
      .then((data) => {
        setQuote(data);
        setError(null);
      })
      .catch((err) => {
        setError(err.message || "Failed to load quotation. The link may be invalid or expired.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, [token]);

  const handleRespond = async (status: "Accepted" | "Rejected") => {
    if (!showCommentBox) {
      setShowCommentBox(status);
      return;
    }
    
    setResponding(true);
    try {
      await publicApi.respond(token, { status, comment });
      setSuccessMsg(`Quotation has been ${status.toLowerCase()} successfully.`);
      if (quote) {
        setQuote({ ...quote, canRespond: false, status: status });
      }
    } catch (err: any) {
      alert(err.message || "An error occurred.");
    } finally {
      setResponding(false);
      setShowCommentBox(null);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="text-gray-500">Loading quotation...</div>
      </div>
    );
  }

  if (error || !quote) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="bg-white p-8 rounded-lg shadow-sm max-w-md w-full text-center">
          <div className="text-red-500 mb-4 text-4xl">⚠</div>
          <h1 className="text-xl font-semibold mb-2">Unavailable</h1>
          <p className="text-gray-600">{error || "Quotation not found"}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-100 py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-4xl mx-auto bg-white shadow-lg rounded-xl overflow-hidden">
        {/*
          The seller's band. This page is the one place the CRM shows itself to
          somebody outside the company, and until the workspace could set a
          colour and a logo it showed them the platform's styling instead of the
          developer they are buying from.
        */}
        <div
          className="h-1.5"
          style={{ background: quote.sellerBrandColor || "#0f172a" }}
        />

        <div className="border-b px-8 py-4 flex items-center gap-3">
          {quote.sellerLogoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={quote.sellerLogoUrl}
              alt={quote.sellerName}
              className="h-9 max-w-[180px] object-contain"
              onError={(event) => {
                // A logo that fails to load must not leave a broken image on a
                // customer's screen; the name alone is a fine fallback.
                event.currentTarget.style.display = "none";
              }}
            />
          ) : null}
          <span className="text-sm font-semibold text-gray-900">
            {quote.sellerName}
          </span>
        </div>

        {/* Header */}
        <div className="border-b px-8 py-6 bg-slate-50 flex justify-between items-start">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">{quote.projectName || "Project"}</h1>
            <p className="text-sm text-gray-500 mt-1">{quote.title}</p>
          </div>
          <div className="text-right">
            <div className="text-sm text-gray-500">Quotation No.</div>
            <div className="text-lg font-semibold">{quote.quoteNumber}</div>
            <div className="text-sm mt-1">
              <span className={`px-2 py-1 rounded text-xs font-medium ${
                quote.status === "Accepted" ? "bg-green-100 text-green-800" :
                quote.status === "Rejected" ? "bg-red-100 text-red-800" :
                "bg-blue-100 text-blue-800"
              }`}>
                {quote.status}
              </span>
            </div>
          </div>
        </div>

        {/* Info Grid */}
        <div className="px-8 py-6 border-b grid grid-cols-2 md:grid-cols-4 gap-6">
          <div>
            <div className="text-xs text-gray-500 uppercase font-semibold">Customer</div>
            <div className="mt-1 font-medium">{quote.customerName}</div>
          </div>
          <div>
            <div className="text-xs text-gray-500 uppercase font-semibold">Unit</div>
            <div className="mt-1 font-medium">
              {quote.unitNumber ? `${quote.towerName ? quote.towerName + " - " : ""}${quote.unitNumber}` : "TBD"}
            </div>
            <div className="text-sm text-gray-500">{quote.unitType}</div>
          </div>
          <div>
            <div className="text-xs text-gray-500 uppercase font-semibold">Area</div>
            <div className="mt-1 font-medium">{formatIndian(quote.saleableArea)} sq.ft.</div>
          </div>
          <div>
            <div className="text-xs text-gray-500 uppercase font-semibold">Date</div>
            <div className="mt-1 font-medium">
              {new Date(quote.issueDate).toLocaleDateString()}
            </div>
            <div className="text-xs text-gray-500 mt-1">
              Valid until: {new Date(quote.validUntil).toLocaleDateString()}
            </div>
          </div>
        </div>

        {quote.isExpired && quote.status === "Issued" && (
          <div className="bg-orange-50 border-l-4 border-orange-400 p-4 mx-8 mt-6">
            <div className="flex">
              <div className="ml-3">
                <p className="text-sm text-orange-700">
                  This quotation expired on {new Date(quote.validUntil).toLocaleDateString()}. Please contact your sales representative.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Pricing */}
        <div className="px-8 py-6">
          <h2 className="text-lg font-semibold border-b pb-2 mb-4">Pricing Breakdown</h2>
          
          <table className="w-full text-sm">
            <tbody className="divide-y">
              <tr className="py-2">
                <td className="py-3 text-gray-600">Basic Rate ({quote.rateCardLabel})</td>
                <td className="py-3 text-right">{formatRupees(quote.ratePerSqft)} / sq.ft.</td>
              </tr>
              {quote.plcPerSqft > 0 && (
                <tr className="py-2">
                  <td className="py-3 text-gray-600">PLC</td>
                  <td className="py-3 text-right">{formatRupees(quote.plcPerSqft)} / sq.ft.</td>
                </tr>
              )}
              {quote.discountPercent > 0 && (
                <tr className="py-2">
                  <td className="py-3 text-gray-600">Discount</td>
                  <td className="py-3 text-right text-green-600">
                    - {formatPercent(quote.discountPercent, 2)} ({formatRupees(quote.effectiveRatePerSqft)} / sq.ft.)
                  </td>
                </tr>
              )}
              <tr className="py-2">
                <td className="py-3 font-medium">Basic Cost</td>
                <td className="py-3 text-right font-medium">{formatRupees(quote.subtotal)}</td>
              </tr>
              <tr className="py-2">
                <td className="py-3 text-gray-600">Taxes ({formatPercent(quote.taxPercent, 1)})</td>
                <td className="py-3 text-right">{formatRupees(quote.taxAmount)}</td>
              </tr>
              
              {quote.charges.map((charge, i) => (
                <tr key={i} className="py-2">
                  <td className="py-3 text-gray-600">{charge.name} {charge.quantity > 1 ? `(x${charge.quantity})` : ''}</td>
                  <td className="py-3 text-right">{formatRupees(charge.totalAmount)}</td>
                </tr>
              ))}

              <tr className="py-3 border-t-2">
                <td className="py-4 text-lg font-bold">Grand Total</td>
                <td className="py-4 text-right text-lg font-bold">{formatRupees(quote.grandTotal)}</td>
              </tr>
            </tbody>
          </table>
          
          {quote.grandTotalInWords && (
            <p className="text-right text-sm text-gray-500 mt-2 italic">
              Rupees {quote.grandTotalInWords} only
            </p>
          )}
        </div>

        {/* Milestones */}
        {quote.milestones.length > 0 && (
          <div className="px-8 py-6 bg-slate-50 border-t">
            <h2 className="text-lg font-semibold mb-4">Payment Schedule: {quote.paymentPlanName}</h2>
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left">
                <thead className="text-xs text-gray-500 uppercase bg-slate-100">
                  <tr>
                    <th className="px-4 py-3 rounded-l-md">Milestone</th>
                    <th className="px-4 py-3">%</th>
                    <th className="px-4 py-3 text-right rounded-r-md">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {quote.milestones.map((m, i) => (
                    <tr key={i} className="bg-white">
                      <td className="px-4 py-3 font-medium text-gray-900">{m.label}</td>
                      <td className="px-4 py-3 text-gray-500">{formatPercent(m.percent, 2)}</td>
                      <td className="px-4 py-3 text-right font-medium">{formatRupees(m.totalAmount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Notes */}
        {quote.notes && (
          <div className="px-8 py-6 border-t">
            <h2 className="text-sm font-semibold mb-2">Terms & Notes</h2>
            <div className="text-sm text-gray-600 whitespace-pre-wrap">{quote.notes}</div>
          </div>
        )}

        {/* Response Actions */}
        {successMsg && (
          <div className="bg-green-50 border-t border-green-200 p-6 text-center text-green-800">
            {successMsg}
          </div>
        )}

        {quote.canRespond && !successMsg && (
          <div className="px-8 py-8 border-t bg-gray-50">
            {showCommentBox ? (
              <div className="max-w-md mx-auto space-y-4">
                <h3 className="font-medium text-center">
                  You are about to {showCommentBox === "Accepted" ? "accept" : "decline"} this quotation.
                </h3>
                <textarea
                  className="w-full border rounded-md p-3 text-sm"
                  rows={3}
                  placeholder="Optional comment..."
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                />
                <div className="flex gap-3 justify-center">
                  <button
                    onClick={() => setShowCommentBox(null)}
                    className="px-4 py-2 border rounded-md bg-white hover:bg-gray-50 text-sm font-medium"
                    disabled={responding}
                  >
                    Cancel
                  </button>
                  <button
                    onClick={() => handleRespond(showCommentBox)}
                    className={`px-6 py-2 rounded-md text-white text-sm font-medium ${
                      showCommentBox === "Accepted" ? "bg-green-600 hover:bg-green-700" : "bg-red-600 hover:bg-red-700"
                    } flex items-center justify-center min-w-[100px]`}
                    disabled={responding}
                  >
                    {responding ? "Wait..." : "Confirm"}
                  </button>
                </div>
              </div>
            ) : (
              <div className="flex justify-center gap-4">
                <button
                  onClick={() => handleRespond("Rejected")}
                  className="px-8 py-2.5 border border-red-200 text-red-600 rounded-md hover:bg-red-50 font-medium transition-colors"
                >
                  Decline
                </button>
                <button
                  onClick={() => handleRespond("Accepted")}
                  className="px-8 py-2.5 bg-gray-900 text-white rounded-md hover:bg-gray-800 font-medium transition-colors"
                >
                  Accept Quotation
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
