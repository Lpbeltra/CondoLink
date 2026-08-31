using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

[Authorize]
public sealed class ManagementCompanyRequestRealtimeHub : Hub { }
