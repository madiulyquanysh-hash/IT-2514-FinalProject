#include "ticketfees.h"

int64_t tf_service_fee_cents(int64_t amount_cents, int32_t quantity, int32_t rate_basis_points, int32_t per_ticket_cents)
{
    if (amount_cents < 0 || amount_cents > TF_MAX_AMOUNT_CENTS) {
        return TF_INVALID_ARGUMENT;
    }

    if (quantity < 1 || quantity > TF_MAX_QUANTITY) {
        return TF_INVALID_ARGUMENT;
    }

    if (rate_basis_points < 0 || rate_basis_points > TF_MAX_RATE_BASIS_POINTS) {
        return TF_INVALID_ARGUMENT;
    }

    if (per_ticket_cents < 0 || per_ticket_cents > TF_MAX_PER_TICKET_CENTS) {
        return TF_INVALID_ARGUMENT;
    }

    int64_t percentage_part = (amount_cents * (int64_t)rate_basis_points + 5000) / 10000;
    int64_t flat_part = (int64_t)quantity * (int64_t)per_ticket_cents;

    return percentage_part + flat_part;
}