using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;
using DemoProvider = CondoLink.Domain.Entities.ServiceProvider;

namespace CondoLink.Api.Features.Overwatch.CommercialDemo;

internal sealed class CommercialDemoCreator(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    LocalFileStorage storage,
    ILogger<CommercialDemoCreator> logger)
{
    private readonly CommercialDemoManifest manifest = new();
    private readonly List<string> createdFiles = [];

    public async Task<CommercialDemoManifest> CreateAsync(
        Guid operatorId, DemoCredentials credentials, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        try
        {
            var ledger = new CommercialDemoDataset(CommercialDemoManifest.DatasetKey, "{}", operatorId);
            db.CommercialDemoDatasets.Add(ledger);
            await db.SaveChangesAsync(ct); // Unique key serializes competing Create calls.

            var manager = await AddUserAsync("Marina Valença", "gestao@aurora.invalid", credentials.ManagerPassword, ct);
            var resident = await AddUserAsync("Camila Nogueira", "camila@aurora.invalid", credentials.ResidentPassword, ct);
            var employee = await AddUserAsync("Lívia Monteiro", "atendimento@horizonte.invalid", credentials.EmployeePassword, ct);
            await AddRoleAsync(manager, "Manager", ct);

            var company = Add(new ManagementCompany("Administradora Horizonte", null,
                "Av. das Acácias, 420", "São Paulo", "SP", "contato@horizonte.invalid", null));
            var condo = Add(new Condominium("Residencial Aurora", "contato@aurora.invalid", null,
                "Rua das Palmeiras, 180", "São Paulo", "SP", true, false, null));
            condo.SetManagementCompany(company.Id);
            condo.ConfigureWhatsAppUpdates(false, null);
            Add(new CondominiumManagementCompanyLink(condo.Id, company.Id));
            foreach (var definition in CondominiumModuleCatalog.All)
                Add(new CondominiumModule(condo.Id, definition.Module,
                    definition.Module != CondominiumModuleType.EmployeeManagement,
                    definition.SupportsManagementCompanyAccess, DateTime.UtcNow));

            var managerMembership = Add(new CondominiumMembership(manager.Id, condo.Id));
            Add(new CondominiumMembershipRole(managerMembership.Id, CondominiumRole.Manager));
            var residentMembership = Add(new CondominiumMembership(resident.Id, condo.Id));
            Add(new CondominiumMembershipRole(residentMembership.Id, CondominiumRole.Resident));
            var blocks = new[] { Add(new CondominiumBlock(condo.Id, "A")), Add(new CondominiumBlock(condo.Id, "B")) };
            var units = new Dictionary<string, Unit>();
            foreach (var block in blocks)
            foreach (var floor in Enumerable.Range(1, 6))
            foreach (var door in Enumerable.Range(1, 2))
            {
                var identifier = $"{floor}{door:00}";
                units[$"{block.Identifier}-{identifier}"] = Add(new Unit(
                    condo.Id, identifier, block.Id, floor.ToString(), null));
            }
            // The 302 storyline uses the A block; the exact same unit is reused in requests and agenda.
            var unit302 = units["A-302"];
            Add(new UnitMembership(resident.Id, unit302.Id, UnitRelationshipType.Owner, true, true));

            var people = new (string Name, string Email, string Unit, UnitRelationshipType Relationship)[]
            {
                ("Rafael Nogueira", "rafael@aurora.invalid", "A-302", UnitRelationshipType.AuthorizedOccupant),
                ("Beatriz Azevedo", "beatriz@aurora.invalid", "A-101", UnitRelationshipType.Owner),
                ("Otávio Reis", "otavio@aurora.invalid", "A-202", UnitRelationshipType.Tenant),
                ("Helena Duarte", "helena@aurora.invalid", "A-401", UnitRelationshipType.Owner),
                ("Pedro Farias", "pedro@aurora.invalid", "A-501", UnitRelationshipType.Tenant),
                ("Ana Luiza Prado", "ana.prado@aurora.invalid", "B-101", UnitRelationshipType.Owner),
                ("João Vilela", "joao@aurora.invalid", "B-202", UnitRelationshipType.Owner),
                ("Clara Martins", "clara@aurora.invalid", "B-301", UnitRelationshipType.Tenant),
                ("André Costa", "andre@aurora.invalid", "B-402", UnitRelationshipType.Owner),
                ("Sofia Almeida", "sofia@aurora.invalid", "B-501", UnitRelationshipType.Tenant),
                ("Luiza Barros", "luiza@aurora.invalid", "B-602", UnitRelationshipType.Owner)
            };
            var residentUsers = new List<ApplicationUser> { resident };
            foreach (var person in people)
            {
                var user = await AddUserAsync(person.Name, person.Email, null, ct);
                residentUsers.Add(user);
                var membership = Add(new CondominiumMembership(user.Id, condo.Id));
                Add(new CondominiumMembershipRole(membership.Id, CondominiumRole.Resident));
                Add(new UnitMembership(user.Id, units[person.Unit].Id, person.Relationship, true, true));
            }

            var categories = new[]
            {
                Add(new Category(condo.Id, "Manutenção", "Reparos e manutenção das áreas comuns")),
                Add(new Category(condo.Id, "Segurança", "Acessos e segurança patrimonial")),
                Add(new Category(condo.Id, "Convivência", "Uso dos espaços e boa convivência")),
                Add(new Category(condo.Id, "Mudanças", "Agendamento e orientações para mudanças")),
                Add(new Category(condo.Id, "Administrativo", "Informações e solicitações administrativas"))
            };
            var providers = new[]
            {
                Add(new DemoProvider("Caio Mendes", "Acesso Seguro", "Portões e automatizadores", null,
                    "+55 11 00000-0000", "caio@acessoseguro.invalid", "caio@acessoseguro.invalid", ServiceProviderPixKeyType.Email,
                    "Atende manutenção preventiva do portão da garagem.", DateTime.UtcNow)),
                Add(new DemoProvider("Renata Bastos", "Água Clara", "Hidráulica", null,
                    "+55 11 00000-0000", "renata@aguaclara.invalid", "renata@aguaclara.invalid", ServiceProviderPixKeyType.Email,
                    "Atendimento a vazamentos e inspeções.", DateTime.UtcNow)),
                Add(new DemoProvider("Diego Rocha", "Luz Viva", "Elétrica", null,
                    "+55 11 00000-0000", "diego@luzviva.invalid", null, null,
                    "Iluminação e quadros elétricos.", DateTime.UtcNow)),
                Add(new DemoProvider("Patrícia Salles", "Jardins do Bairro", "Jardinagem", null,
                    "+55 11 00000-0000", "patricia@jardins.invalid", null, null,
                    "Cuidado periódico das áreas verdes.", DateTime.UtcNow))
            };
            providers[2].SetStatus(false, DateTime.UtcNow);
            foreach (var provider in providers)
            {
                Add(new ServiceProviderCondominiumLink(provider.Id, condo.Id));
                Add(new ServiceProviderSpecialty(provider.Id, provider.Specialty));
            }

            var stories = new (string Title, string Description, int Category, string Unit, int Resident,
                RequestStatus Status, RequestPriority Priority, RequestSource Source, int? Provider,
                string Reply)[]
            {
                ("Portão da garagem falhando", "O portão fechou antes de o carro terminar de passar. Poderiam verificar o sensor?", 1, "A-302", 0,
                    RequestStatus.WaitingForThirdParty, RequestPriority.Urgent, RequestSource.WhatsApp, 0,
                    "Acionamos o técnico de automatizadores. A visita está prevista para amanhã pela manhã."),
                ("Vazamento no corredor do 2º andar", "Há água próxima à porta do elevador no bloco A desde cedo.", 0, "A-202", 3,
                    RequestStatus.InProgress, RequestPriority.High, RequestSource.Portal, 1,
                    "A equipe de hidráulica vai inspecionar a tubulação hoje à tarde."),
                ("Lâmpada apagada no hall", "A luz do hall do bloco B está apagada desde ontem.", 0, "B-101", 6,
                    RequestStatus.Resolved, RequestPriority.Normal, RequestSource.Portal, 2,
                    "A lâmpada foi substituída e o hall já está iluminado."),
                ("Orientação para mudança", "Vamos receber os móveis no sábado. Como reservamos o elevador de serviço?", 3, "B-301", 8,
                    RequestStatus.WaitingForResident, RequestPriority.Normal, RequestSource.Portal, null,
                    "Enviei as orientações. Pode confirmar o horário desejado?"),
                ("Ruído após o horário de silêncio", "O som no apartamento vizinho ficou alto depois das 23h.", 2, "A-401", 4,
                    RequestStatus.WaitingForManager, RequestPriority.Normal, RequestSource.Portal, null,
                    "Registramos o relato e vamos conversar com os envolvidos com discrição."),
                ("Interfone da entrada social", "O interfone da portaria toca, mas não conseguimos ouvir a resposta.", 0, "B-202", 7,
                    RequestStatus.InProgress, RequestPriority.High, RequestSource.Portal, null,
                    "Vamos testar o equipamento da portaria e retornar com um diagnóstico."),
                ("Uso do salão de festas", "Onde encontro as regras e horários de uso do salão?", 2, "B-402", 9,
                    RequestStatus.Resolved, RequestPriority.Normal, RequestSource.Portal, null,
                    "As orientações estão no Regimento Interno, disponível em Documentos."),
                ("Sugestão de bicicletário", "Seria possível estudar mais vagas para bicicletas no térreo?", 2, "B-501", 10,
                    RequestStatus.InProgress, RequestPriority.Normal, RequestSource.Portal, null,
                    "Obrigado pela sugestão. Vamos levar o tema à próxima reunião de gestão."),
                ("Poda preventiva do jardim", "Os galhos perto do acesso de pedestres precisam de poda.", 0, "A-501", 5,
                    RequestStatus.WaitingForThirdParty, RequestPriority.Normal, RequestSource.Portal, 3,
                    "A jardineira fará uma avaliação na próxima visita programada."),
                ("Segunda via de comunicado", "Poderiam encaminhar novamente o comunicado sobre a manutenção da água?", 4, "B-602", 11,
                    RequestStatus.Resolved, RequestPriority.Normal, RequestSource.Portal, null,
                    "Claro. O comunicado foi disponibilizado na área de documentos.")
            };
            var requests = new List<DomainRequest>();
            for (var index = 0; index < stories.Length; index++)
            {
                var story = stories[index];
                var openedAt = DateTime.UtcNow.AddDays(-(2 + index * 5));
                var request = Add(new DomainRequest(condo.Id, residentUsers[story.Resident].Id,
                    units[story.Unit].Id, categories[story.Category].Id, story.Title, story.Description, story.Source));
                db.Entry(request).Property(x => x.CreatedAt).CurrentValue = openedAt;
                requests.Add(request);
                var firstMessage = Add(new RequestMessage(request.Id, request.AuthorUserId, story.Description,
                    story.Source == RequestSource.WhatsApp ? MessageChannel.WhatsApp : MessageChannel.Portal));
                db.Entry(firstMessage).Property(x => x.CreatedAt).CurrentValue = openedAt.AddMinutes(3);
                var reply = Add(new RequestMessage(request.Id, manager.Id, story.Reply));
                db.Entry(reply).Property(x => x.CreatedAt).CurrentValue = openedAt.AddHours(3);
                Add(new RequestStatusHistory(request.Id, null, RequestStatus.InProgress,
                    request.AuthorUserId, "Atendimento aberto pelo morador.", openedAt));
                if (story.Priority != RequestPriority.Normal)
                    request.ChangePriority(story.Priority, openedAt.AddHours(1));
                if (story.Status != RequestStatus.InProgress)
                {
                    request.ChangeStatus(story.Status, openedAt.AddDays(1));
                    Add(new RequestStatusHistory(request.Id, RequestStatus.InProgress, story.Status,
                        manager.Id, "Atualização da gestão.", openedAt.AddDays(1)));
                }
                if (story.Provider is int providerIndex)
                {
                    var provider = providers[providerIndex];
                    request.SetServiceProvider(provider.Id, openedAt.AddDays(1));
                    Add(new RequestServiceProviderHistory(request.Id, "Linked", null, null,
                        provider.Name, provider.Specialty, manager.Id, openedAt.AddDays(1)));
                }
                if (story.Status == RequestStatus.InProgress && story.Provider is null)
                    db.Entry(request).Property(x => x.UpdatedAt).CurrentValue = openedAt.AddHours(3);
            }
            Add(new RequestInternalNote(requests[0].Id, manager.Id,
                "Conferir o sensor antes de liberar o acesso de veículos."));
            Add(new RequestInternalNote(requests[1].Id, manager.Id,
                "Isolar o ponto úmido até identificar a origem do vazamento."));
            Add(new RequestInternalNote(requests[4].Id, manager.Id,
                "Tratar o relato de convivência sem expor o morador."));
            await AddAttachmentAsync(requests[0].Id, manager.Id, "relato-do-portao.txt",
                "Relato fictício: o sensor do portão interrompeu o fechamento durante a entrada de veículo.", ct);
            await AddAttachmentAsync(requests[1].Id, manager.Id, "inspecao-do-corredor.txt",
                "Registro fictício: umidade observada junto ao elevador do segundo andar do bloco A.", ct);

            var reminders = new[]
            {
                Add(new AgendaReminder(condo.Id, manager.Id, "Retorno do técnico do portão",
                    "Confirmar teste do sensor com Caio Mendes.", unit302.Id, "Acesso Seguro",
                    DateTime.UtcNow.AddDays(1), "America/Sao_Paulo", AgendaRecurrenceType.None, false, false, DateTime.UtcNow)),
                Add(new AgendaReminder(condo.Id, manager.Id, "Acompanhar vazamento no bloco A",
                    "Verificar a área após o reparo hidráulico.", units["A-202"].Id, "Água Clara",
                    DateTime.UtcNow.AddDays(2), "America/Sao_Paulo", AgendaRecurrenceType.None, false, false, DateTime.UtcNow)),
                Add(new AgendaReminder(condo.Id, manager.Id, "Inspeção preventiva da iluminação",
                    "Revisar halls e escadarias dos dois blocos.", null, "Luz Viva",
                    DateTime.UtcNow.AddDays(12), "America/Sao_Paulo", AgendaRecurrenceType.Monthly, false, false, DateTime.UtcNow)),
                Add(new AgendaReminder(condo.Id, manager.Id, "Conferir iluminação do hall",
                    "Confirmar a substituição da lâmpada no bloco B.", units["B-101"].Id, "Luz Viva",
                    DateTime.UtcNow.AddDays(-7), "America/Sao_Paulo", AgendaRecurrenceType.None, false, false, DateTime.UtcNow))
            };
            reminders[3].Complete(DateTime.UtcNow.AddDays(-5));
            Add(new AgendaReminderRequest(reminders[0].Id, requests[0].Id, manager.Id, DateTime.UtcNow));
            Add(new AgendaReminderRequest(reminders[1].Id, requests[1].Id, manager.Id, DateTime.UtcNow));

            var access = Add(new ManagementCompanyEmployee(company.Id, employee.Id, "Atendimento condominial"));
            var adminCategory = Add(new ManagementCompanyRequestCategory(company.Id,
                "Orientações administrativas", "Dúvidas da gestão e orientações operacionais",
                ManagementCompanyRequestFormType.Generic));
            Add(new ManagementCompanyRequestCategoryResponsible(adminCategory.Id, access.Id));
            var adminOpenedAt = DateTime.UtcNow.AddDays(-3);
            var adminRequest = Add(new ManagementCompanyRequest(condo.Id, company.Id,
                adminCategory.Id, manager.Id, ManagementCompanyRequestType.GeneralQuestion,
                "ADM-AUR-0041", adminOpenedAt));
            Add(new ManagementCompanyGeneralQuestionRequest(adminRequest.Id, "Revisão do contrato de manutenção"));
            var adminQuestion = Add(new ManagementCompanyRequestMessage(adminRequest.Id, manager.Id,
                "Olá, podem confirmar a data de renovação do contrato de manutenção dos elevadores?"));
            db.Entry(adminQuestion).Property(x => x.CreatedAt).CurrentValue = adminOpenedAt.AddMinutes(2);
            var adminReply = Add(new ManagementCompanyRequestMessage(adminRequest.Id, employee.Id,
                "Estamos conferindo o contrato e retornaremos com a data e os próximos passos."));
            db.Entry(adminReply).Property(x => x.CreatedAt).CurrentValue = adminOpenedAt.AddDays(1);
            Add(new ManagementCompanyRequestHistory(adminRequest.Id, ManagementCompanyRequestEventType.Created,
                null, ManagementCompanyRequestStatus.Submitted, manager.Id, null, DateTime.UtcNow.AddDays(-3)));
            adminRequest.Acknowledge(employee.Id, DateTime.UtcNow.AddDays(-2));
            Add(new ManagementCompanyRequestHistory(adminRequest.Id, ManagementCompanyRequestEventType.Acknowledged,
                ManagementCompanyRequestStatus.Submitted, ManagementCompanyRequestStatus.Acknowledged,
                employee.Id, null, DateTime.UtcNow.AddDays(-2)));
            var completedAdminOpenedAt = DateTime.UtcNow.AddDays(-18);
            var completedAdminRequest = Add(new ManagementCompanyRequest(condo.Id, company.Id,
                adminCategory.Id, manager.Id, ManagementCompanyRequestType.GeneralQuestion,
                "ADM-AUR-0036", completedAdminOpenedAt));
            Add(new ManagementCompanyGeneralQuestionRequest(completedAdminRequest.Id,
                "Calendário de manutenção preventiva"));
            var completedAdminQuestion = Add(new ManagementCompanyRequestMessage(completedAdminRequest.Id, manager.Id,
                "Podem nos ajudar a organizar as datas das inspeções preventivas deste semestre?"));
            db.Entry(completedAdminQuestion).Property(x => x.CreatedAt).CurrentValue = completedAdminOpenedAt.AddMinutes(2);
            var completedAdminReply = Add(new ManagementCompanyRequestMessage(completedAdminRequest.Id, employee.Id,
                "Claro. O calendário atualizado foi compartilhado com a gestão."));
            db.Entry(completedAdminReply).Property(x => x.CreatedAt).CurrentValue = completedAdminOpenedAt.AddDays(3);
            Add(new ManagementCompanyRequestHistory(completedAdminRequest.Id, ManagementCompanyRequestEventType.Created,
                null, ManagementCompanyRequestStatus.Submitted, manager.Id, null, DateTime.UtcNow.AddDays(-18)));
            completedAdminRequest.Acknowledge(employee.Id, DateTime.UtcNow.AddDays(-17));
            Add(new ManagementCompanyRequestHistory(completedAdminRequest.Id, ManagementCompanyRequestEventType.Acknowledged,
                ManagementCompanyRequestStatus.Submitted, ManagementCompanyRequestStatus.Acknowledged,
                employee.Id, null, DateTime.UtcNow.AddDays(-17)));
            completedAdminRequest.TransitionTo(ManagementCompanyRequestStatus.InProgress, DateTime.UtcNow.AddDays(-16));
            Add(new ManagementCompanyRequestHistory(completedAdminRequest.Id, ManagementCompanyRequestEventType.StatusChanged,
                ManagementCompanyRequestStatus.Acknowledged, ManagementCompanyRequestStatus.InProgress,
                employee.Id, null, DateTime.UtcNow.AddDays(-16)));
            completedAdminRequest.Complete(employee.Id, DateTime.UtcNow.AddDays(-14));
            Add(new ManagementCompanyRequestHistory(completedAdminRequest.Id, ManagementCompanyRequestEventType.Completed,
                ManagementCompanyRequestStatus.InProgress, ManagementCompanyRequestStatus.Completed,
                employee.Id, null, DateTime.UtcNow.AddDays(-14)));

            await AddDocumentAsync(condo.Id, manager.Id, "Regimento Interno — Residencial Aurora",
                CondominiumDocumentType.InternalRules, "regimento-interno-aurora.txt",
                "Residencial Aurora — Regimento Interno (material fictício de demonstração).\n" +
                "Mudanças devem ser agendadas com a gestão com 48 horas de antecedência. " +
                "O elevador de serviço pode ser reservado de segunda a sábado, das 8h às 18h. " +
                "O salão de festas pode ser utilizado até as 22h, mediante reserva. " +
                "O horário de silêncio começa às 22h. Prestadores devem se identificar na portaria.", ct);
            await AddDocumentAsync(condo.Id, manager.Id, "Manual de Mudanças",
                CondominiumDocumentType.Manual, "manual-de-mudancas-aurora.txt",
                "Residencial Aurora — Manual de Mudanças (material fictício de demonstração).\n" +
                "Solicite a reserva do elevador de serviço à gestão. Informe bloco, unidade, dia e horário. " +
                "Proteja as paredes e o piso durante o transporte. A portaria deve receber a lista dos profissionais.", ct);

            ledger.SetManifest(manifest.ToJson());
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Commercial demo {Key} created by {OperatorId}; {EntityCount} records.",
                CommercialDemoManifest.DatasetKey, operatorId, manifest.Entities.Values.Sum(x => x.Count));
            return manifest;
        }
        catch
        {
            foreach (var file in createdFiles) storage.Delete(file);
            throw;
        }
    }

    private T Add<T>(T entity) where T : class
    {
        db.Set<T>().Add(entity);
        manifest.Record(entity, db);
        return entity;
    }

    private async Task<ApplicationUser> AddUserAsync(string name, string email, string? password, CancellationToken ct)
    {
        if (!email.EndsWith(".invalid", StringComparison.Ordinal))
            throw new InvalidOperationException("Demo email must use the reserved .invalid domain.");
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException($"Demo identity {email} already exists outside the dataset.");
        var user = new ApplicationUser(name, email, null);
        user.SetReceiveWhatsAppUpdates(false);
        var result = password is null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Demo user creation failed: " +
                string.Join(", ", result.Errors.Select(x => x.Code)));
        manifest.Record(user, db);
        ct.ThrowIfCancellationRequested();
        return user;
    }

    private async Task AddRoleAsync(ApplicationUser user, string roleName, CancellationToken ct)
    {
        var role = await roles.FindByNameAsync(roleName);
        if (role is null)
        {
            role = new IdentityRole<Guid>(roleName);
            var created = await roles.CreateAsync(role);
            if (!created.Succeeded) throw new InvalidOperationException("Demo role creation failed.");
            manifest.Record(role, db);
        }
        var result = await users.AddToRoleAsync(user, roleName);
        if (!result.Succeeded) throw new InvalidOperationException("Demo role assignment failed.");
        manifest.Roles.Add(new DemoRoleLink(user.Id, role.Id));
        ct.ThrowIfCancellationRequested();
    }

    private async Task AddDocumentAsync(Guid condoId, Guid managerId, string name,
        CondominiumDocumentType type, string fileName, string content, CancellationToken ct)
    {
        var document = Add(new CondominiumDocument(condoId, name, type, fileName,
            "pending", "text/plain", 1, DateOnly.FromDateTime(DateTime.UtcNow), managerId));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var key = await storage.SaveCondominiumDocumentAsync(condoId, document.Id, stream, ".txt", ct);
        createdFiles.Add(key);
        document.SetStorageKey(key);
        var vector = await new LocalEmbeddingService().EmbedAsync(content, ct);
        var chunk = Add(new CondominiumDocumentChunk(document.Id, condoId, 0, content,
            JsonSerializer.Serialize(vector), null, name));
        Add(DocumentKnowledgeBuilder.Build(document, [chunk]));
        document.Ready();
    }

    private async Task AddAttachmentAsync(Guid requestId, Guid managerId,
        string fileName, string content, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await using var stream = new MemoryStream(bytes);
        var key = await storage.SaveAsync(requestId, stream, ".txt", ct);
        createdFiles.Add(key);
        Add(new RequestAttachment(requestId, managerId, fileName, key, "text/plain", bytes.Length));
    }
}

internal sealed record DemoCredentials(string ManagerPassword, string ResidentPassword, string EmployeePassword);
