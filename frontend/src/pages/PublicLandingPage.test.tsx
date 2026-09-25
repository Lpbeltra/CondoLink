import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it, vi } from "vitest";
import { PublicLandingPage } from "./PublicLandingPage";

describe("PublicLandingPage", () => {
  it("presents the first commercial narrative with real product imagery", () => {
    render(
      <MemoryRouter>
        <PublicLandingPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByRole("heading", {
        level: 1,
        name: /Seu condomínio já se comunica\. O Comvy organiza o que acontece depois\./i,
      }),
    ).toBeVisible();
    expect(within(screen.getByRole("banner")).getByRole("link", { name: "Acessar o Comvy" })).toHaveAttribute("href", "/login");
    expect(screen.getByAltText(/Dashboard real do Comvy/i)).toHaveAttribute(
      "src",
      "/marketing/aurora/dashboard.png",
    );
    expect(screen.getByAltText(/Atendimento real no Comvy/i)).toHaveAttribute(
      "src",
      "/marketing/aurora/atendimento-portao-historico.png",
    );
    expect(screen.getByRole("link", { name: "Comvy — início" })).toHaveAttribute("href", "#inicio");
    expect(screen.getByRole("link", { name: "Como funciona" })).toHaveAttribute("href", "#como-funciona");
    expect(screen.getByRole("link", { name: "Assistente" })).toHaveAttribute("href", "#assistente");
    expect(screen.queryByRole("link", { name: "Produto" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Contato" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Quero conhecer" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Conheça como funciona" })).not.toBeInTheDocument();
    expect(screen.queryByText("Contato comercial")).not.toBeInTheDocument();
    expect(screen.queryByText("O canal de contato comercial ainda não está disponível nesta página.")).not.toBeInTheDocument();
  });

  it("keeps every commercial CTA on a real local destination", () => {
    const { container } = render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    for (const link of container.querySelectorAll("a")) {
      const href = link.getAttribute("href")!;
      if (href.startsWith("#")) expect(container.querySelector(href)).not.toBeNull();
      else expect(href).toBe("/login");
    }
    expect(screen.getByRole("img", { name: /Mensagem de áudio/ })).toBeVisible();
  });

  it("continues the same gate request through context, people, provider and agenda", () => {
    const { container } = render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    const regions = screen.getAllByRole("region");
    const titles = regions.map((region) => within(region).queryAllByRole("heading", { level: 2 })[0]?.textContent);
    expect(titles.slice(-5)).toEqual([
      "Tudo o que aconteceu.No lugar em que aconteceu.",
      "A mesma situação.A informação certa para cada pessoa.",
      "Sua memória não deveria ser a ferramenta de gestão do condomínio.",
      "Saiba quem está falando — e de onde vem cada solicitação.",
      "Resolver também é acompanhar o que acontece depois.",
    ]);
    const perspectives = screen.getByRole("region", { name: /A mesma situação/ });
    expect(within(perspectives).getByAltText(/Perspectiva da gestão/)).toHaveAttribute("src", "/marketing/aurora/atendimento-portao-historico.png");
    expect(within(perspectives).getByAltText(/Perspectiva de Camila/)).toHaveAttribute("src", "/marketing/aurora/atendimento-portao-moradora.png");
    const relationships = screen.getByRole("region", { name: /Saiba quem está falando/ });
    expect(within(relationships).getByAltText(/Unidades reais/)).toHaveAttribute("src", "/marketing/aurora/unidades.png");
    expect(within(relationships).getByAltText(/Cadastro real de Camila/)).toHaveAttribute("src", "/marketing/aurora/moradores.png");
    const followthrough = screen.getByRole("region", { name: /Resolver também/ });
    expect(within(followthrough).getByAltText(/Aba Prestador/)).toHaveAttribute("src", "/marketing/aurora/atendimento-portao-prestador.png");
    expect(within(followthrough).getByAltText(/Agenda real/)).toHaveAttribute("src", "/marketing/aurora/agenda.png");
    expect(within(followthrough).getByRole("heading", { name: /Da mensagem.*ao próximo passo/ })).toBeVisible();
    for (const crop of container.querySelectorAll(".landing-aurora-crop img")) {
      expect(crop).toHaveAttribute("loading", "lazy");
      expect(crop).toHaveAttribute("alt", expect.any(String));
    }
    expect(container.querySelectorAll(".landing-operation a, .landing-operation button, .landing-operation form")).toHaveLength(0);
  });

  it("links to the actual walkthrough and introduces the real assistant after the organized context", () => {
    render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    expect(screen.getByRole("link", { name: "Como funciona" })).toHaveAttribute("href", "#como-funciona");
    const walkthrough = screen.getByRole("region", { name: /Do “síndico, conseguiu ver/ });
    const editorialTarget = document.getElementById("como-funciona")!;
    expect(walkthrough).toContainElement(editorialTarget);
    expect(editorialTarget).toContainElement(screen.getByRole("heading", { name: /Do “síndico, conseguiu ver/ }));
    expect(editorialTarget).not.toContainElement(screen.getByAltText(/Atendimento real no Comvy/));
    expect(screen.getByRole("link", { name: "Assistente" })).toHaveAttribute("href", "#assistente");
    expect(screen.getByRole("region", { name: "Pergunte ao seu condomínio." })).toHaveAttribute("id", "assistente");
    expect(screen.getByAltText(/Assistente real do Comvy/)).toHaveAttribute("src", "/marketing/aurora/assistente.png");
    expect(screen.getByAltText(/Assistente real do Comvy/)).toHaveAttribute("loading", "lazy");
    expect(screen.queryByRole("link", { name: "Recursos" })).not.toBeInTheDocument();
  });

  it.each(["inicio", "assistente", "como-funciona"])("restores #%s after React mounts a directly loaded fragment", (id) => {
    const scrollIntoView = vi.fn();
    const original = HTMLElement.prototype.scrollIntoView;
    HTMLElement.prototype.scrollIntoView = scrollIntoView;
    window.history.replaceState(null, "", `/#${id}`);
    try {
      const { container } = render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
      expect(scrollIntoView).toHaveBeenCalledOnce();
      expect(scrollIntoView.mock.instances[0]).toBe(container.querySelector(`#${id}`));
      expect(scrollIntoView).toHaveBeenCalledWith({ block: "start", behavior: "instant" });
      expect(container.querySelector(`#${id}`)).toHaveClass(id === "como-funciona" ? "landing-editorial-target" : "landing-scene");
    } finally {
      window.history.replaceState(null, "", "/");
      HTMLElement.prototype.scrollIntoView = original;
    }
  });

  it("uses the same target for header clicks and hash changes, without intercepting modified clicks", () => {
    const scrollIntoView = vi.fn();
    const original = HTMLElement.prototype.scrollIntoView;
    HTMLElement.prototype.scrollIntoView = scrollIntoView;
    try {
      render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
      const link = screen.getByRole("link", { name: "Como funciona" });
      fireEvent.click(link);
      expect(window.location.hash).toBe("#como-funciona");
      expect(scrollIntoView.mock.instances.at(-1)).toBe(document.getElementById("como-funciona"));
      scrollIntoView.mockClear();
      fireEvent.click(link, { ctrlKey: true });
      expect(scrollIntoView).not.toHaveBeenCalled();
      window.history.replaceState(null, "", "/#assistente");
      fireEvent(window, new HashChangeEvent("hashchange"));
      expect(scrollIntoView.mock.instances.at(-1)).toBe(document.getElementById("assistente"));
      window.history.replaceState(null, "", "/#%invalid");
      expect(() => fireEvent(window, new HashChangeEvent("hashchange"))).not.toThrow();
    } finally {
      window.history.replaceState(null, "", "/");
      HTMLElement.prototype.scrollIntoView = original;
    }
  });

  it("cancels the pending fragment alignment when the landing unmounts", () => {
    const frames = new Map<number, FrameRequestCallback>();
    let frameId = 0;
    vi.spyOn(window, "requestAnimationFrame").mockImplementation((callback) => {
      frames.set(++frameId, callback);
      return frameId;
    });
    const cancel = vi.spyOn(window, "cancelAnimationFrame");
    const previous = window.history.scrollRestoration;
    const { unmount } = render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    act(() => frames.get(1)!(0));
    unmount();
    expect(cancel).toHaveBeenCalledWith(2);
    expect(window.history.scrollRestoration).toBe(previous);
  });

  it("releases a settled entry for small gestures in either direction and rearms on return", () => {
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true } as MediaQueryList);
    let position = 1000;
    vi.spyOn(window, "scrollY", "get").mockImplementation(() => position);
    const { unmount } = render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    const target = document.getElementById("como-funciona")!;
    vi.spyOn(target, "getBoundingClientRect").mockImplementation(() => ({ top: 1000 - position } as DOMRect));
    const scrollTo = (value: number) => {
      position = value;
      fireEvent.scroll(window);
    };
    fireEvent(window, new Event("scrollend"));
    expect(target).toHaveAttribute("data-reading");
    scrollTo(1072);
    scrollTo(1144);
    expect(target).toHaveAttribute("data-reading");
    scrollTo(1072);
    expect(target).not.toHaveAttribute("data-reading");
    scrollTo(1000);
    fireEvent(window, new Event("scrollend"));
    scrollTo(928);
    scrollTo(856);
    expect(target).toHaveAttribute("data-reading");
    scrollTo(928);
    expect(target).not.toHaveAttribute("data-reading");
    scrollTo(1000);
    fireEvent(window, new Event("scrollend"));
    scrollTo(1000 + window.innerHeight);
    expect(target).not.toHaveAttribute("data-reading");
    unmount();
    fireEvent(window, new Event("scrollend"));
    expect(target).not.toHaveAttribute("data-reading");
  });

  it("reveals compositions once and releases observation on unmount", () => {
    let reveal: IntersectionObserverCallback = () => {};
    const observe = vi.fn();
    const unobserve = vi.fn();
    const disconnect = vi.fn();
    vi.stubGlobal("IntersectionObserver", class {
      constructor(callback: IntersectionObserverCallback) { reveal = callback; }
      observe = observe;
      unobserve = unobserve;
      disconnect = disconnect;
    });
    const { container, unmount } = render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    const composition = container.querySelector("[data-landing-reveal]")!;
    expect(observe).toHaveBeenCalledWith(composition);
    reveal([{ target: composition, isIntersecting: false } as IntersectionObserverEntry], {} as IntersectionObserver);
    expect(composition).not.toHaveClass("landing-in-view");
    reveal([{ target: composition, isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
    expect(composition).toHaveClass("landing-in-view");
    expect(unobserve).toHaveBeenCalledWith(composition);
    unmount();
    expect(disconnect).toHaveBeenCalledOnce();
  });

  it("keeps content available without starting motion when reduced motion is requested", () => {
    const observer = vi.fn();
    vi.stubGlobal("IntersectionObserver", observer);
    vi.spyOn(window, "matchMedia").mockReturnValue({ matches: true } as MediaQueryList);
    render(<MemoryRouter><PublicLandingPage /></MemoryRouter>);
    expect(observer).not.toHaveBeenCalled();
    expect(screen.getByRole("heading", { level: 1 })).toBeVisible();
    expect(screen.getByAltText(/Atendimento real no Comvy/)).toBeVisible();
    expect(screen.getByAltText(/Agenda real/)).toBeVisible();
    expect(screen.getByRole("heading", { name: /Da mensagem.*ao próximo passo/ })).toBeVisible();
  });
});
