using System.Runtime.InteropServices;

namespace TicketBookingSystem.Infrastructure;

internal static class NativeMethods
{
    private const string LibraryName = "ticketfees";

    [DllImport(LibraryName, EntryPoint = "tf_service_fee_cents", CallingConvention = CallingConvention.Cdecl)]
    internal static extern long ServiceFeeCents(long amountCents, int quantity, int rateBasisPoints, int perTicketCents);
}