EventStore.TestClientAPI
========================

This program is a clone of the [Event Store TestClient](https://github.com/EventStore/EventStore/tree/dev/src/EventStore.TestClient) that uses the TCP Client API instead of sending the raw network messages.  It performs almost as fast as the original test client.  Currently only the Write Flood (WRFL) command is implemented and the output does not match the original.  Usage:

    EventStore.TestClientAPI.exe WRFL [<clients> <requests> [<streams-cnt> [<size>]]]


 * clients - the number of threads to run at one time.  Default: 1
 * requests - the total number of events to write across ALL clients. Default: 5000
 * streams-cnt - the number of streams to distribute the events across. Default: 1000
 * size - the size of the event data to write. The total event size will be size+132 due to meta data and json formatting. Default: 256

Limitations
-----------
No connection settings can be changed. This client always connects to 127.0.0.1:1113 and uses the default username and password.
