# Stage 0 — Infrastructure & Local Dev Composition

**Goal:** Make local runs mirror production topology early, establishing the dependency graph, observability spine, and configuration validation that all later stages depend on.

## Current State

The codebase already includes:
- Garnet (Redis-compatible cache) via `Aspire.StackExchange.Redis`
- Redpanda (Kafka-compatible) via KafkaFlow with `{prefix}-audio-events` and `{prefix}-track-deletions` topics
- Basic health endpoints (`/health`, `/alive`) in `ServiceDefaults`
  outages% A simple, self-contained CV (no external .cls required).
  \documentclass[9pt]{article}

\usepackage[T1]{fontenc}
\usepackage[utf8]{inputenc}
\usepackage[left=0.4in,top=0.35in,right=0.4in,bottom=0.35in]{geometry}
\usepackage[hidelinks]{hyperref}
\usepackage{enumitem}

\setlength{\parindent}{0pt}
\setlist[itemize]{leftmargin=*,nosep}
\setlength{\tabcolsep}{0pt}

\newcommand{\cvname}[1]{\textbf{\LARGE #1}\par}
\newcommand{\cvline}[1]{#1\par}
\newcommand{\cvsection}[1]{\vspace{0.5\baselineskip}\textbf{\large #1}\par\vspace{0.1\baselineskip}\hrule\vspace{0.3\baselineskip}}
\newcommand{\cvrole}[3]{\textbf{#1}\hfill #2\par\textit{#3}\par}
\newcommand{\jobsep}{\vspace{0.2\baselineskip}\hrule height 0.2pt\vspace{0.2\baselineskip}}

\begin{document}
\setlength{\parskip}{0pt}
\linespread{0.92}\selectfont

    \cvname{Roman Markevich}
    \cvline{\textit{Senior C\# / ASP.NET Core Developer \quad\textbar\quad 8+ years experience}}
    \cvline{Limassol, Cyprus}
    \cvline{Mobile: +44 7715 450685 \quad\textbar\quad Email: \href{mailto:markevich.roma@gmail.com}{markevich.roma@gmail.com} \quad\textbar\quad GitHub: \href{https://github.com/Tassadar2499}{github.com/Tassadar2499} \quad\textbar\quad LinkedIn: \href{https://www.linkedin.com/in/roman-markevich}{linkedin.com/in/roman-markevich}}

    \cvsection{Summary}
    \begin{itemize}
        \item Senior C\# / ASP.NET Core developer with 8+ years building backend systems for marketplaces and distributed products.
        \item T-shaped specialist: deep in .NET and distributed systems, broad in architecture, analytics/observability, testing, and technical leadership.
        \item Own delivery end-to-end: requirements, design decisions, implementation, CI/CD, release, and production support.
        \item Business-minded team player: align engineering work with measurable outcomes (SLOs, cost, lead time) and collaborate across teams and stakeholders.
    \end{itemize}

    \cvsection{Skills}
    \small
    \begin{itemize}
        \item \textbf{Languages:} C\#, Python, Bash, PowerShell
        \item \textbf{.NET:} .NET 7--9, ASP.NET Core, Entity Framework Core, Dapper, Hangfire
        \item \textbf{Testing:} xUnit, Testcontainers, FluentAssertions, Verify; integration and functional testing (BDD)
        \item \textbf{Datastores:} PostgreSQL, MongoDB, Microsoft SQL Server, ClickHouse, Redis
        \item \textbf{Messaging \& APIs:} Kafka, RabbitMQ, MassTransit; HTTP/REST, gRPC/Protobuf
        \item \textbf{Cloud/Platform:} AWS, Azure; Kubernetes; Docker, Docker Compose
        \item \textbf{Tooling:} Git; GitLab CI/CD, GitHub Actions
        \item \textbf{Observability:} Elasticsearch/Logstash/Kibana (ELK), Grafana, Prometheus, Jaeger
        \item \textbf{Infra/Security:} Nginx; Linux, Windows Server; etcd, HashiCorp Vault
        \item \textbf{Architecture:} BPMN, UML, C4
        \item \textbf{Leadership/Product:} mentoring, design reviews, cross-team coordination; KPI/SLO-driven prioritization and trade-offs
    \end{itemize}
    \normalsize

    \cvsection{Experience}
    \cvrole{Gold Apple}{Sep 2023--Present (1.5 yrs)}{Senior C\# / ASP.NET Core Developer --- E-commerce marketplace}
    \begin{itemize}
        \item Reduced production incidents and bugs in core business rules by introducing BDD-style functional tests and specification-driven development with product/QA
        \item Shortened incident MTTR 90 min $\rightarrow$ 30 min (-67\%) by leading rollout of end-to-end observability (structured logs, distributed tracing, Grafana dashboards, SLO-based alerts) across 12 critical services
        \item Reduced message-loss incidents 6 $\rightarrow$ 1 per quarter (-83\%) and improved message-delivery SLO 99.70\% $\rightarrow$ 99.95\% by implementing a MongoDB$\rightarrow$Kafka Transactional Outbox and upstreaming it into the open-source KafkaFlow library
        \item Increased event-processing throughput by cutting Kafka topic lag 3--4$\times$ via batch consumption for high-throughput consumers
    \end{itemize}
    \cvline{\textbf{Technologies:} C\#, .NET 9; ASP.NET Core; MongoDB; Elasticsearch, Logstash, Kibana; Grafana, Prometheus, Jaeger; gRPC; Redis; in-memory cache; AWS}

    \jobsep

    \cvrole{Katusha IT}{Feb 2022--Sep 2023 (1.5 yrs)}{Senior C\# / ASP.NET Core Developer --- Distributed printing platform}
    \begin{itemize}
        \item Reduced rework across teams by producing architectural diagrams and facilitating design reviews with the lead architect
        \item Reduced operational risk during rollout by cutting rollout incidents through standardized deployment and rollback practices for Kubernetes services
        \item Increased delivery speed by reducing release lead time 8 hours $\rightarrow$ 20 minutes via CI/CD pipelines (Bash, PowerShell)
        \item Improved availability at peak from 99.50\% $\rightarrow$ 99.90\% while sustaining about 900 jobs/min by enabling a legacy-to-microservices migration on Kubernetes with fault-tolerant design
    \end{itemize}
    \cvline{\textbf{Technologies:} C\#, .NET 8; ASP.NET Core; Entity Framework Core; PostgreSQL; ClickHouse; RabbitMQ; MassTransit; Kubernetes; Azure}

    \jobsep

    \cvrole{Ozon Tech}{Feb 2019--Feb 2022 (3 yrs)}{Senior C\# / ASP.NET Core Developer --- E-commerce marketplace}
    \begin{itemize}
        \item Improved customer-facing read latency by 2.5$\times$ by splitting a bottleneck service and scaling reads independently
        \item Achieved 0 outages over 18 months and reduced migration-related major incidents by decomposing a monolith via the Strangler Pattern and incremental traffic shifting
        \item Cut flaky test rate 15\% $\rightarrow$ 4\% by building an integration testing framework with disposable PostgreSQL environments in Docker
        \item Increased load-test throughput 2{,}000 RPS $\rightarrow$ 6{,}000 RPS and reduced setup time 45 min $\rightarrow$ 10 min by improving load-test fidelity with dynamic test-data generation and reusable performance tooling
        \item Enabled a storage-cost reduction roadmap (-20\% storage cost; 30~TB shifted to cold tier) by planning PostgreSQL sharding and hot/cold data separation
    \end{itemize}
    \cvline{\textbf{Technologies:} C\#, .NET 7; ASP.NET Core; Entity Framework Core; PostgreSQL; Kafka; gRPC; etcd; HashiCorp Vault}

    \jobsep

    \cvrole{Directum RX}{Jan 2018--Jan 2019 (1 yr)}{Junior/Middle C\# Developer --- Enterprise DMS}

    \cvsection{Education}
    \cvrole{Novosibirsk State University}{2017--2024}{Math and Computer Science / Software Engineering}
    \begin{itemize}
        \item Bachelor's Degree --- Math and Computer Science (2017--2021)
        \item Master's Degree --- Software Engineering (2022--2024)
    \end{itemize}

\end{document}

