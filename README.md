# Crestron-SmartThings
Crestron Simpl# Module for Smart Things

This is a Crestron Simpl# Module to Interface with a Smart Things Hub.
-The following things are required:
-The SmartThings SmartApp installed w/ Oath 2.0 Authentication Enabled.
-The Receiver module in your Simpl Program.
-A Port forwarded to your processor of your choosing, you will need to specify this in the Smart App and on the Receiver Module.
-The receiver module will automatically detect your public IP Address.
-Load the Simpl Program.
-Open Text Console via Toolbox.
-Pulse the Authentication signal on the receiver module.
-This will give you a link to visit to authorize your processor to talk to SmartThings.
-Visit this link in your web browser, and grant access to the devices you want to control.
-This procedure only has to be done once, the credentials will be stored in a file in NVRAM.
-If you add more hardware to SmartThings you will have to redo this procedure to grant access.
-On each Control Module you specify the DeviceID.
-This can be found via the SmartThings Web IDE or by pulsing the Print Device List on the Receiver Module while watching Text Console.
-You should now have two way communication between Crestron and SmartThings. 
