import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import React from "react";
import {
  GameHubEvents,
  GameHubMethods,
  GameHubRoutes,
  type ActionAppliedMessage,
  type AiThinkingMessage,
  type SessionUpdatedMessage,
} from "../api/generated/contracts";

/** Handlers for the events of one session; all optional. */
export type GameSessionHandlers = {
  onActionApplied?: (message: ActionAppliedMessage) => void;
  onAiThinking?: (message: AiThinkingMessage) => void;
  onSessionUpdated?: (message: SessionUpdatedMessage) => void;
  /** Called after a lost connection was re-established; reload the state, events may have been missed. */
  onReconnected?: () => void;
};

let connection: HubConnection | null = null;
let starting: Promise<void> | null = null;
const joinedSessions = new Map<string, number>();
const reconnectListeners = new Set<() => void>();

/** The one shared connection of the app (cookie authentication, automatic reconnect). */
const getConnection = (): HubConnection => {
  if (!connection) {
    connection = new HubConnectionBuilder()
      .withUrl(GameHubRoutes.Path)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .configureLogging(LogLevel.Warning)
      .build();

    // Group memberships are lost with the old connection: join all sessions again.
    connection.onreconnected(() => {
      joinedSessions.forEach((_, sessionId) => {
        void connection?.invoke(GameHubMethods.JoinSession, sessionId);
      });
      reconnectListeners.forEach((listener) => listener());
    });
  }

  return connection;
};

const ensureStarted = async (): Promise<HubConnection> => {
  const hub = getConnection();
  if (hub.state === HubConnectionState.Disconnected) {
    starting ??= hub.start().finally(() => {
      starting = null;
    });
  }
  if (starting) {
    await starting;
  }
  return hub;
};

const joinSession = async (sessionId: string): Promise<boolean> => {
  const hub = await ensureStarted();
  joinedSessions.set(sessionId, (joinedSessions.get(sessionId) ?? 0) + 1);
  return hub.invoke<boolean>(GameHubMethods.JoinSession, sessionId);
};

const leaveSession = async (sessionId: string): Promise<void> => {
  const count = (joinedSessions.get(sessionId) ?? 1) - 1;
  if (count > 0) {
    joinedSessions.set(sessionId, count);
    return;
  }

  joinedSessions.delete(sessionId);
  if (connection?.state === HubConnectionState.Connected) {
    await connection.invoke(GameHubMethods.LeaveSession, sessionId);
  }
};

/**
 * Subscribe to the real-time events of a session while the component is mounted.
 * Returns whether the subscription is active (false while connecting or if not a participant).
 */
export const useGameSessionEvents = (sessionId: string | null | undefined, handlers: GameSessionHandlers): boolean => {
  const [subscribedId, setSubscribedId] = React.useState<string | null>(null);
  const handlersRef = React.useRef(handlers);

  React.useEffect(() => {
    handlersRef.current = handlers;
  });

  React.useEffect(() => {
    if (!sessionId) {
      return;
    }

    let active = true;
    const hub = getConnection();
    const forSession =
      <TMessage extends { sessionId: string }>(callback: (message: TMessage) => void) =>
      (message: TMessage) => {
        if (message.sessionId === sessionId) {
          callback(message);
        }
      };

    const onActionApplied = forSession<ActionAppliedMessage>((m) => handlersRef.current.onActionApplied?.(m));
    const onAiThinking = forSession<AiThinkingMessage>((m) => handlersRef.current.onAiThinking?.(m));
    const onSessionUpdated = forSession<SessionUpdatedMessage>((m) => handlersRef.current.onSessionUpdated?.(m));
    const onReconnected = () => handlersRef.current.onReconnected?.();

    hub.on(GameHubEvents.ActionApplied, onActionApplied);
    hub.on(GameHubEvents.AiThinking, onAiThinking);
    hub.on(GameHubEvents.SessionUpdated, onSessionUpdated);
    reconnectListeners.add(onReconnected);

    joinSession(sessionId)
      .then((joined) => {
        if (active) {
          setSubscribedId(joined ? sessionId : null);
        }
      })
      .catch(() => {
        if (active) {
          setSubscribedId(null);
        }
      });

    return () => {
      active = false;
      reconnectListeners.delete(onReconnected);
      hub.off(GameHubEvents.ActionApplied, onActionApplied);
      hub.off(GameHubEvents.AiThinking, onAiThinking);
      hub.off(GameHubEvents.SessionUpdated, onSessionUpdated);
      void leaveSession(sessionId);
    };
  }, [sessionId]);

  return subscribedId !== null && subscribedId === sessionId;
};
