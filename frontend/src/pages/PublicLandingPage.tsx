import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import PlayArrowRoundedIcon from "@mui/icons-material/PlayArrowRounded";
import { useEffect, useRef, type MouseEvent } from "react";
import { Brand } from "../components/Brand";
import { Link } from "react-router-dom";
import "./PublicLandingPage.css";

const messages = [
  { sender: "+55 11 9••••-••••", text: "Oi, conseguiu ver aquilo?", time: "09:12" },
  { sender: "Camila · 302", text: "O portão fechou antes do carro passar.", time: "09:14" },
  { sender: "Mariana · 1204", kind: "audio", time: "09:16" },
  { sender: "Carlos", text: "Síndico, mandei o documento semana passada.", time: "09:21" },
  { sender: "Ana · 703", kind: "document", time: "09:23" },
  { sender: "+55 11 9••••-••••", text: "É sobre aquele vazamento que te falei.", time: "11:48" },
];

// Shared by the small hero fragments and the conversation examples. These are
// illustrations of messages, not interactive audio players or file downloads.
function MessageAttachment({ kind }: { kind: "audio" | "document" }) {
  if (kind === "document") {
    return (
      <div className="landing-attachment">
        <DescriptionOutlinedIcon aria-hidden="true" />
        <span>documento.pdf<small>Documento recebido</small></span>
      </div>
    );
  }
  return (
    <div className="landing-audio" role="img" aria-label="Mensagem de áudio de 3 minutos e 47 segundos">
      <PlayArrowRoundedIcon aria-hidden="true" />
      <span className="landing-audio__wave" aria-hidden="true">
        {Array.from({ length: 25 }, (_, index) => <i key={index} />)}
      </span>
      <span aria-hidden="true">3:47</span>
    </div>
  );
}

function MarketingHeader({ onNavigate }: { onNavigate: (id: string) => void }) {
  const followAnchor = (event: MouseEvent<HTMLAnchorElement>, id: string) => {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    onNavigate(id);
  };
  return (
    <header className="landing-header">
      <div className="landing-header__inner">
        <a className="landing-brand" href="#inicio" aria-label="Comvy — início" onClick={(event) => followAnchor(event, "inicio")}>
          {/* Replace this single integration point with the approved main logo's
              navy-background SVG when supplied. Do not trace the reference board. */}
          <Brand />
        </a>
        <nav className="landing-nav" aria-label="Navegação principal">
          <a href="#como-funciona" onClick={(event) => followAnchor(event, "como-funciona")}>Como funciona</a>
          <a href="#assistente" onClick={(event) => followAnchor(event, "assistente")}>Assistente</a>
        </nav>
        <div className="landing-header__actions">
          <Link className="landing-button landing-button--text" to="/login">
            Acessar o Comvy
          </Link>
        </div>
      </div>
    </header>
  );
}

function HeroSection() {
  return (
    <section className="landing-hero landing-scene" id="inicio" aria-labelledby="landing-hero-title">
      <div className="landing-shell landing-hero__composition">
        <div className="landing-hero__copy">
          <p className="landing-eyebrow">Comunicação que vira gestão.</p>
          <h1 id="landing-hero-title">
            Seu condomínio já se comunica.
            <span>O Comvy organiza o que acontece depois.</span>
          </h1>
          <p className="landing-hero__support">
            O Comvy transforma mensagens, solicitações e conversas do dia a dia em atendimentos
            organizados, com contexto, histórico e acompanhamento.
          </p>
          <div className="landing-hero__actions" aria-label="Acessar o produto">
            <Link className="landing-button landing-button--outline landing-button--large" to="/login">
              Acessar o Comvy
            </Link>
          </div>
          <p className="landing-hero__audience">
            Para síndicos e administradoras que querem organizar a comunicação sem complicar a vida de quem mora.
          </p>
        </div>

        <div className="landing-hero__visual" data-landing-reveal>
          <div className="landing-hero__fragments" aria-hidden="true">
            <div className="landing-fragment landing-fragment--question">
              <span>+55 11 9••••-••••</span>
              <p>Oi, conseguiu ver aquilo?</p>
            </div>
            <div className="landing-fragment landing-fragment--audio">
              <span>Mariana · 1204</span>
              <MessageAttachment kind="audio" />
            </div>
            <div className="landing-fragment landing-fragment--document">
              <span>Ana · 703</span>
              <MessageAttachment kind="document" />
            </div>
            <div className="landing-fragment landing-fragment--gate">
              <span>Camila · 302</span>
              <p>O portão fechou antes do carro passar.</p>
            </div>
            <div className="landing-fragment-connection" />
          </div>
          <div className="landing-product">
            <div className="landing-product__label">
              <span>Contexto para agir.</span>
              <span>Residencial Aurora · Produto real</span>
            </div>
            <figure className="landing-product__frame">
              <div className="landing-product__viewport">
                <img
                  src="/marketing/aurora/dashboard.png"
                  width="2880"
                  height="2000"
                  alt="Dashboard real do Comvy no Residencial Aurora, com indicadores de atendimentos, urgências e principais motivos"
                  fetchPriority="high"
                />
              </div>
              <figcaption>
                Uma visão operacional clara do que está aberto, urgente e precisa avançar.
              </figcaption>
            </figure>
          </div>
        </div>
        <div className="landing-hero__footnote">
          <span>Mensagens, áudios, documentos.</span>
          <p>Fragmentos do dia a dia.<br /><strong>Uma operação com contexto.</strong></p>
        </div>
      </div>
    </section>
  );
}

function CommunicationChaosSection() {
  return (
    <section className="landing-chaos landing-scene" aria-labelledby="landing-chaos-title">
      <div className="landing-shell landing-chaos__inner">
        <div className="landing-chaos__heading">
          <p className="landing-section-kicker">Antes do Comvy</p>
          <h2 id="landing-chaos-title">Se você é síndico, provavelmente conhece essa conversa.</h2>
        </div>
        <div className="landing-message-field" aria-label="Exemplos de mensagens dispersas" data-landing-reveal>
          {messages.map((message, index) => (
            <article className={`landing-message landing-message--${index + 1}`} key={`${message.sender}-${index}`}>
              <span className="landing-message__sender">{message.sender}</span>
              {message.kind === "audio" || message.kind === "document"
                ? <MessageAttachment kind={message.kind} />
                : <p>{message.text}</p>}
              <time>{message.time}</time>
            </article>
          ))}
        </div>
        <div className="landing-chaos__closing">
          <p>Dezenas de mensagens. Vários assuntos. Todos importantes para alguém.</p>
          <strong>E você precisa lembrar de tudo.</strong>
        </div>
      </div>
    </section>
  );
}

function WhatsAppThesisSection() {
  return (
    <section className="landing-thesis" id="conversa-e-gestao" aria-labelledby="landing-thesis-title">
      <div className="landing-shell landing-thesis__inner">
        <p className="landing-section-kicker landing-section-kicker--blue">O ponto de virada</p>
        <h2 id="landing-thesis-title">
          O problema não é conversar pelo WhatsApp.
          <span>É tentar administrar um condomínio por ele.</span>
        </h2>
        <p>
          O WhatsApp continua sendo uma forma simples e familiar de conversar. O que o Comvy faz é dar
          estrutura ao que acontece depois dessa conversa.
        </p>
        <div className="landing-thesis__rule" aria-hidden="true" />
        <strong>O morador não precisa aprender a sua gestão para conseguir falar com ela.</strong>
      </div>
    </section>
  );
}

function CommunicationTransformationSection() {
  return (
    <section className="landing-transformation landing-scene" aria-labelledby="landing-transformation-title">
      <div className="landing-shell">
        <div className="landing-transformation__heading landing-editorial-target" id="como-funciona">
          <p className="landing-section-kicker landing-section-kicker--blue">Mensagem → Atendimento</p>
          <h2 id="landing-transformation-title">
            Do “síndico, conseguiu ver?” ao acompanhamento de verdade.
          </h2>
          <p>Uma conversa simples entra. O contexto operacional aparece para a gestão.</p>
        </div>

        <div className="landing-transformation__flow" data-landing-reveal>
          <div className="landing-resident-side">
            <p className="landing-flow-label">Uma mensagem</p>
            <article className="landing-resident-message">
              <div className="landing-avatar" aria-hidden="true">CN</div>
              <div>
                <span>Camila · 302</span>
                <p>O portão fechou antes de o carro terminar de passar. Poderiam verificar o sensor?</p>
                <time>16:09</time>
              </div>
            </article>
            <div className="landing-context-note">
              <p className="landing-flow-label">Encontra contexto</p>
              <dl>
                <div><dt>Pessoa e unidade</dt><dd>Camila Nogueira<br />Bloco A · 302</dd></div>
                <div><dt>Assunto</dt><dd>Portão da garagem falhando</dd></div>
              </dl>
            </div>
          </div>

          <figure className="landing-attendance">
            <div className="landing-attendance__topline">
              <span>Contexto em um atendimento</span>
              <em>Atendimento real</em>
            </div>
            <div className="landing-attendance__viewport">
              <img
                src="/marketing/aurora/atendimento-portao-historico.png"
                width="2880"
                height="2000"
                loading="lazy"
                alt="Atendimento real no Comvy para falha no portão, com moradora, unidade, categoria, prioridade, histórico, prestador e contexto"
              />
            </div>
            <figcaption><span>Histórico preservado.</span> Contexto para a próxima ação.</figcaption>
          </figure>
        </div>

        <p className="landing-mantra">
          <span>O morador fala.</span>
          <span>O Comvy organiza.</span>
          <span>A gestão acompanha.</span>
        </p>
      </div>
    </section>
  );
}

function AssistantTeaser() {
  return (
    <section className="landing-assistant landing-scene" id="assistente" aria-labelledby="landing-assistant-title">
      <div className="landing-shell landing-assistant__inner">
        <div className="landing-assistant__copy">
          <p className="landing-section-kicker landing-section-kicker--blue">Assistente do Comvy</p>
          <h2 id="landing-assistant-title">Pergunte ao seu condomínio.</h2>
          <p>Quando a informação está organizada, você pode simplesmente perguntar.</p>
        </div>
        <figure className="landing-assistant__figure">
          <div className="landing-assistant__viewport">
            <img
              src="/marketing/aurora/assistente.png"
              width="2880"
              height="2000"
              loading="lazy"
              alt="Assistente real do Comvy no Residencial Aurora, com sugestões para consultar atendimentos, moradores, agenda e prestadores"
            />
          </div>
          <figcaption>Assistente · Residencial Aurora · Tela real do produto</figcaption>
        </figure>
      </div>
    </section>
  );
}

// All crops use the original 2880 × 2000 showroom captures. Labels remain
// outside the image, so editorial emphasis never changes the product itself.
function AuroraCrop({ image, alt, className }: { image: string; alt: string; className: string }) {
  return (
    <div className={`landing-aurora-crop ${className}`}>
      <img src={`/marketing/aurora/${image}.png`} width="2880" height="2000" loading="lazy" decoding="async" alt={alt} />
    </div>
  );
}

function AttendanceHistorySection() {
  return (
    <section className="landing-history landing-operation" aria-labelledby="landing-history-title">
      <div className="landing-shell">
        <div className="landing-history__heading">
          <div>
            <p className="landing-section-kicker landing-section-kicker--blue">Depois da mensagem · Atendimento</p>
            <h2 id="landing-history-title">Tudo o que aconteceu.<br />No lugar em que aconteceu.</h2>
          </div>
          <p className="landing-operation__support">A conversa deixa de ser uma sequência perdida de mensagens. Ganha contexto, histórico e acompanhamento.</p>
        </div>
        <figure className="landing-history__figure" data-landing-reveal>
          <div className="landing-operation__caption"><span>O portão do Residencial Aurora</span><span>Atendimento #981FBAB4</span></div>
          <AuroraCrop image="atendimento-portao-historico" className="landing-history__crop" alt="Histórico do portão: relato de Camila Nogueira, resposta de Marina Valença, vínculo com Caio Mendes e status Aguardando terceiro" />
          <figcaption><strong>O relato, a resposta, o que foi feito.</strong><span>Histórico, anexos e notas pertencem ao mesmo atendimento.</span></figcaption>
        </figure>
      </div>
    </section>
  );
}

function AttendancePerspectivesSection() {
  return (
    <section className="landing-perspectives landing-operation" aria-labelledby="landing-perspectives-title">
      <div className="landing-shell">
        <div className="landing-perspectives__heading">
          <p className="landing-section-kicker">Duas perspectivas · A mesma situação</p>
          <h2 id="landing-perspectives-title">A mesma situação.<br />A informação certa para cada pessoa.</h2>
        </div>
        <div className="landing-perspectives__composition" data-landing-reveal>
          <figure className="landing-perspectives__management">
            <figcaption><strong>Para quem administra</strong><span>Decisões e contexto para conduzir o atendimento.</span></figcaption>
            <AuroraCrop image="atendimento-portao-historico" className="landing-perspectives__management-crop" alt="Perspectiva da gestão: ações para atualizar o atendimento, alterar status e prioridade, resolver e abrir lembrete" />
          </figure>
          <figure className="landing-perspectives__resident">
            <figcaption><strong>Para quem mora</strong><span>Camila acompanha a própria solicitação.</span></figcaption>
            <AuroraCrop image="atendimento-portao-moradora" className="landing-perspectives__resident-crop" alt="Perspectiva de Camila Nogueira: Portão da garagem falhando, Bloco A, 302, aguardando terceiro, com resumo do relato e sem as ações internas da gestão" />
            <p className="landing-perspectives__reference">Mesmo portão. Mesmo atendimento. <span>#981FBAB4</span></p>
          </figure>
        </div>
        <p className="landing-perspectives__closing">Transparência para quem mora.<br /><span>Contexto para quem administra.</span></p>
      </div>
    </section>
  );
}

function OperationalMemorySection() {
  return (
    <section className="landing-memory landing-operation" aria-labelledby="landing-memory-title">
      <div className="landing-shell landing-memory__inner" data-landing-reveal>
        <p className="landing-section-kicker landing-section-kicker--blue">O contexto fica.</p>
        <div>
          <h2 id="landing-memory-title">Sua memória não deveria ser a ferramenta de gestão do condomínio.</h2>
          <p className="landing-operation__support">Quando a comunicação ganha contexto, o restante da operação começa a se conectar.</p>
        </div>
        <p className="landing-memory__questions"><span>Quem falou?</span><span>De qual unidade?</span><span>Quem está cuidando?</span><span>Qual é o próximo passo?</span></p>
      </div>
    </section>
  );
}

function ResidentsAndUnitsSection() {
  return (
    <section className="landing-relationships landing-operation" aria-labelledby="landing-relationships-title">
      <div className="landing-shell landing-relationships__composition">
        <div className="landing-relationships__heading">
          <p className="landing-section-kicker landing-section-kicker--blue">Uma pessoa. Um lugar.</p>
          <h2 id="landing-relationships-title">Saiba quem está falando — e de onde vem cada solicitação.</h2>
          <p className="landing-operation__support">Moradores, unidades e vínculos fazem parte do contexto da gestão. Você não precisa reconstruir essa relação a cada conversa.</p>
        </div>
        <figure className="landing-relationships__units" data-landing-reveal>
          <figcaption>Residencial Aurora <span>Unidades organizadas por bloco</span></figcaption>
          <AuroraCrop image="unidades" className="landing-relationships__units-crop" alt="Unidades reais do Residencial Aurora organizadas em blocos A e B; apartamento 302 do Bloco A com dois moradores" />
        </figure>
        <div className="landing-relationships__person" data-landing-reveal>
          <p className="landing-flow-label">A pessoa por trás da solicitação</p>
          <figure>
            <AuroraCrop image="moradores" className="landing-relationships__person-crop" alt="Cadastro real de Camila Nogueira, vinculada ao Bloco A, unidade 302, como proprietária" />
            <figcaption>Camila Nogueira <span>Bloco A · 302</span></figcaption>
          </figure>
          <p className="landing-relationships__connection">É a Camila do atendimento do portão.<br /><strong>A conversa já tem endereço.</strong></p>
        </div>
      </div>
    </section>
  );
}

function ProviderAndAgendaSection() {
  return (
    <section className="landing-followthrough landing-operation" aria-labelledby="landing-followthrough-title">
      <div className="landing-shell">
        <div className="landing-followthrough__heading">
          <p className="landing-section-kicker landing-section-kicker--blue">O atendimento continua</p>
          <h2 id="landing-followthrough-title">Resolver também é acompanhar o que acontece depois.</h2>
          <p className="landing-operation__support">Quem vai resolver, o que foi combinado e quando voltar ao assunto. O próximo passo faz parte da gestão.</p>
        </div>
        <div className="landing-followthrough__sequence">
          <figure className="landing-followthrough__provider" data-landing-reveal>
            <figcaption><span className="landing-flow-label">Quem está cuidando</span><strong>Caio Mendes · Acesso Seguro</strong><span>O prestador vinculado ao atendimento do portão.</span></figcaption>
            <AuroraCrop image="atendimento-portao-prestador" className="landing-followthrough__provider-crop" alt="Aba Prestador do atendimento do portão com Caio Mendes, da Acesso Seguro, especialista em portões e automatizadores" />
          </figure>
          <figure className="landing-followthrough__agenda" data-landing-reveal>
            <figcaption><span className="landing-flow-label">Quando acompanhar</span><strong>O retorno já tem lugar na agenda.</strong><span>Confirmar o teste do sensor, sem perder o vínculo com o atendimento.</span></figcaption>
            <AuroraCrop image="agenda" className="landing-followthrough__agenda-crop" alt="Agenda real: Retorno do técnico do portão, confirmar teste do sensor com Caio Mendes, Bloco A, apartamento 302, Acesso Seguro, vinculado ao Atendimento #981FBAB4" />
          </figure>
        </div>
        <div className="landing-followthrough__closing">
          <p>A conversa encontra contexto.<br />E o que vem depois continua sendo acompanhado.</p>
          <h2>Da mensagem<br /><span>ao próximo passo.</span></h2>
        </div>
      </div>
    </section>
  );
}

export function PublicLandingPage() {
  const rootRef = useRef<HTMLDivElement>(null);

  const navigateToAnchor = (id: string, updateHistory = true) => {
    const target = document.getElementById(id);
    const root = rootRef.current;
    if (!target || !root?.contains(target)) return;

    if (updateHistory && window.location.hash !== `#${id}`) {
      window.history.pushState(null, "", `#${id}`);
    }
    target.scrollIntoView({ block: "start", behavior: "instant" });
  };

  useEffect(() => {
    // The browser may restore an old scroll coordinate after React resolves the fragment.
    const previousScrollRestoration = window.history.scrollRestoration;
    window.history.scrollRestoration = "manual";
    const restoreFragment = () => {
      let id;
      try { id = decodeURIComponent(window.location.hash.slice(1)); }
      catch { return; }
      if (id) navigateToAnchor(id, false);
    };
    restoreFragment();
    let restoreAfterPaint: number | undefined;
    const restoreAfterLayout = window.requestAnimationFrame(() => {
      restoreAfterPaint = window.requestAnimationFrame(restoreFragment);
    });
    window.addEventListener("hashchange", restoreFragment);
    return () => {
      window.removeEventListener("hashchange", restoreFragment);
      window.cancelAnimationFrame(restoreAfterLayout);
      if (restoreAfterPaint !== undefined) window.cancelAnimationFrame(restoreAfterPaint);
      window.history.scrollRestoration = previousScrollRestoration;
    };
  }, []);

  useEffect(() => {
    const target = rootRef.current?.querySelector<HTMLElement>(".landing-editorial-target");
    if (!target) return;
    const desktopSnap = window.matchMedia("(min-width: 1024px) and (prefers-reduced-motion: no-preference)");
    let previousY = window.scrollY;
    let departureDirection = 0;
    const distanceFromEntry = () => target.getBoundingClientRect().top
      - (parseFloat(window.getComputedStyle(target).scrollMarginTop) || 0);
    const releaseEntry = () => {
      // Snap helps arrive. After resting, release this target so even a short
      // forward gesture can read the tall scene. No scroll position is changed.
      if (desktopSnap.matches && Math.abs(distanceFromEntry()) < 2
        && !target.hasAttribute("data-reading")) {
        departureDirection = 0;
        target.setAttribute("data-reading", "");
      }
    };
    const followReading = () => {
      const currentY = window.scrollY;
      const direction = Math.sign(currentY - previousY);
      // Rearm on a return toward the entry, or once outside its proximity area.
      if (!desktopSnap.matches || Math.abs(distanceFromEntry()) > window.innerHeight / 2
        || (departureDirection && direction && direction !== departureDirection)) {
        target.removeAttribute("data-reading");
        departureDirection = 0;
      } else if (target.hasAttribute("data-reading") && direction) {
        departureDirection = direction;
      }
      previousY = currentY;
    };
    releaseEntry();
    window.addEventListener("scrollend", releaseEntry);
    window.addEventListener("scroll", followReading, { passive: true });
    return () => {
      window.removeEventListener("scrollend", releaseEntry);
      window.removeEventListener("scroll", followReading);
      target.removeAttribute("data-reading");
    };
  }, []);

  useEffect(() => {
    if (!rootRef.current || typeof IntersectionObserver === "undefined"
      || window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    // Content is visible by default, including when observation is unavailable.
    // Each composition plays once; no scroll loop or sticky animation timeline.
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add("landing-in-view");
          observer.unobserve(entry.target);
        }
      }
    }, { threshold: 0.15 });
    rootRef.current.querySelectorAll("[data-landing-reveal]").forEach((element) => observer.observe(element));
    return () => observer.disconnect();
  }, []);

  return (
    <div className="public-landing" ref={rootRef}>
      <MarketingHeader onNavigate={(id) => navigateToAnchor(id)} />
      <main>
        <HeroSection />
        <CommunicationChaosSection />
        <WhatsAppThesisSection />
        <CommunicationTransformationSection />
        <AssistantTeaser />
        <AttendanceHistorySection />
        <AttendancePerspectivesSection />
        <OperationalMemorySection />
        <ResidentsAndUnitsSection />
        <ProviderAndAgendaSection />
      </main>
    </div>
  );
}
