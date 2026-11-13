namespace ModernTinyWall.TinyWall
{
    // Possible message types from controller to service
    public enum MessageType
    {
        // General responses
        INVALID_COMMAND,
        RESPONSE_ERROR,
        RESPONSE_LOCKED,
        COM_ERROR,

        // Read commands (>31)
        GET_SETTINGS = 32,
        GET_PROCESS_PATH,
        READ_FW_LOG,
        IS_LOCKED,
        READ_PENDING_CONNECTIONS = 36,
        READ_DATA_USAGE = 37,
        READ_BLOCKED_TRAFFIC = 38,

        // Unprivileged write commands (>1023)
        UNLOCK = 1024,

        // Privileged write commands (>2047)
        MODE_SWITCH = 2048,
        REINIT,
        PUT_SETTINGS,
        LOCK,
        SET_PASSPHRASE,
        STOP_SERVICE,
        MINUTE_TIMER,
        REENUMERATE_ADDRESSES,
        RESOLVE_PENDING_CONNECTION = 2057,

        // Service-to-client messages
        DATABASE_UPDATED = 2056,

        // Service-to-service only (>4095)
        ADD_TEMPORARY_EXCEPTION = 4096,
        RELOAD_WFP_FILTERS,
        DISPLAY_POWER_EVENT,
    }
}
