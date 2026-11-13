#ifndef TICKETFEES_H
#define TICKETFEES_H

#include <stdint.h>

#if defined(_WIN32)
#define TF_EXPORT __declspec(dllexport)
#else
#define TF_EXPORT __attribute__((visibility("default")))
#endif

#define TF_MAX_AMOUNT_CENTS INT64_C(1000000000000)
#define TF_MAX_QUANTITY 1000000
#define TF_MAX_RATE_BASIS_POINTS 10000
#define TF_MAX_PER_TICKET_CENTS 100000
#define TF_INVALID_ARGUMENT INT64_C(-1)

#ifdef __cplusplus
extern "C" {
#endif

TF_EXPORT int64_t tf_service_fee_cents(int64_t amount_cents, int32_t quantity, int32_t rate_basis_points, int32_t per_ticket_cents);

#ifdef __cplusplus
}
#endif

#endif